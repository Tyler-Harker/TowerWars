using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TowerWars.Shared.Constants;
using TowerWars.Shared.DTOs;
using TowerWars.Shared.Protocol;
using TowerWars.ZoneServer.Services;

namespace TowerWars.ZoneServer.Game;

public sealed class GameSession
{
    private readonly ILogger _logger;
    private readonly SessionPlayerManager _playerManager;
    private readonly IEventPublisher _eventPublisher;
    private readonly ITowerBonusService _towerBonusService;
    private readonly Action<uint, IPacket> _send;
    private readonly Action<IPacket> _broadcast;
    private readonly Random _random = new();

    private readonly Dictionary<uint, Tower> _towers = new();
    private readonly Dictionary<uint, Unit> _units = new();
    private readonly Dictionary<uint, ServerItemDrop> _itemDrops = new();
    private readonly object _entitiesLock = new();
    private readonly ConcurrentQueue<Action> _pendingActions = new();

    // Track XP per player tower (by PlayerTowerId)
    private readonly Dictionary<Guid, int> _towerXpAccumulated = new();
    // Track purchase count per player tower (by PlayerTowerId) for cost scaling
    private readonly Dictionary<Guid, int> _towerPurchaseCounts = new();
    // Track kills this wave for perfect wave bonus
    private int _unitsKilledThisWave;
    private int _unitsLeakedThisWave;

    private uint _nextEntityId;
    private uint _currentTick;
    private int _currentWave;
    private GameState _state = GameState.WaitingForPlayers;
    private readonly Guid _matchId;

    public Guid MatchId => _matchId;
    public GameState State => _state;
    public int CurrentWave => _currentWave;
    public SessionPlayerManager PlayerManager => _playerManager;

    private bool _isPaused;
    private string? _pauseReason;
    private GameMode _gameMode = GameMode.Solo;

    // Auto-start timing for solo mode
    private float _autoStartDelay = -1f;
    private float _waveStartDelay = -1f;

    public bool IsPaused => _isPaused;
    public string? PauseReason => _pauseReason;

    public event Action<GameSession>? OnSessionEnded;

    public GameSession(
        Guid matchId,
        ILogger logger,
        SessionPlayerManager playerManager,
        IEventPublisher eventPublisher,
        ITowerBonusService towerBonusService,
        Action<uint, IPacket> send,
        Action<IPacket> broadcast)
    {
        _matchId = matchId;
        _logger = logger;
        _playerManager = playerManager;
        _eventPublisher = eventPublisher;
        _towerBonusService = towerBonusService;
        _send = send;
        _broadcast = broadcast;

        _playerManager.OnAllPlayersReady += HandleAllPlayersReady;
        _playerManager.OnPlayerJoined += HandlePlayerJoined;
    }

    private void HandlePlayerJoined(SessionPlayer player)
    {
        _logger.LogInformation("Player {PlayerId} joined, current state: {State}", player.PlayerId, _state);
        // Match starts when the player sends Ready (via HandleAllPlayersReady),
        // not on connection — since the client may connect early for data loading.
    }

    public void Update(float deltaTime)
    {
        _currentTick++;

        // Process pending actions from async operations (must run on game loop thread)
        while (_pendingActions.TryDequeue(out var action))
        {
            action();
        }

        // Skip game logic updates when paused
        if (_isPaused)
            return;

        // Handle auto-start delay for solo mode
        if (_autoStartDelay > 0)
        {
            _autoStartDelay -= deltaTime;
            if (_autoStartDelay <= 0)
            {
                _autoStartDelay = -1f;
                var map = GenerateDefaultMap();
                StartMatch(GameMode.Solo, map);
                _waveStartDelay = 3f; // Start first wave after 3 seconds
            }
        }

        // Handle wave start delay
        if (_waveStartDelay > 0)
        {
            _waveStartDelay -= deltaTime;
            if (_waveStartDelay <= 0)
            {
                _waveStartDelay = -1f;
                StartWave();
            }
        }

        switch (_state)
        {
            case GameState.WaitingForPlayers:
                break;

            case GameState.Preparation:
                break;

            case GameState.WaveActive:
                UpdateUnits(deltaTime);
                UpdateTowers(deltaTime);
                CheckWaveCompletion();
                break;

            case GameState.GameOver:
                break;
        }
    }

    public void Tick()
    {
        // Skip tick updates when paused
        if (_isPaused)
            return;

        if (_state == GameState.WaveActive && _currentTick % 3 == 0)
        {
            BroadcastEntityUpdates();
        }
    }

    /// <summary>
    /// Pause or resume the game. Used for connection issues or manual pause.
    /// </summary>
    public void SetPaused(bool paused, string? reason = null)
    {
        if (_isPaused == paused)
            return;

        _isPaused = paused;
        _pauseReason = reason;

        _broadcast(new GamePausePacket
        {
            IsPaused = paused,
            Reason = reason
        });

        _logger.LogInformation("Game {State}: {Reason}",
            paused ? "paused" : "resumed",
            reason ?? "no reason specified");

        _eventPublisher.PublishAsync(new
        {
            EventType = paused ? "game.paused" : "game.resumed",
            MatchId = _matchId,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        });
    }

    public void ProcessInput(uint peerId, PlayerInputPacket packet)
    {
        var player = _playerManager.GetPlayer(peerId);
        if (player == null) return;

        player.LastProcessedInputSequence = packet.InputSequence;

        _send(peerId, new PlayerInputAckPacket
        {
            LastProcessedSequence = packet.InputSequence
        });
    }

    public async void ProcessTowerBuild(uint peerId, TowerBuildPacket packet)
    {
        var player = _playerManager.GetPlayer(peerId);
        if (player == null) return;

        var baseStats = TowerDefinitions.GetStats(packet.TowerType);

        // Calculate dynamic cost based on purchase count for this player tower
        var purchaseCount = _towerPurchaseCounts.GetValueOrDefault(packet.PlayerTowerId, 0);
        var dynamicCost = TowerCostCalculator.CalculateCost(packet.TowerType, purchaseCount);

        if (player.Gold < dynamicCost)
        {
            SendError(peerId, ErrorCode.InsufficientGold, "Not enough gold", packet.RequestId);
            return;
        }

        if (!IsValidPlacement(packet.GridX, packet.GridY, player.PlayerId))
        {
            SendError(peerId, ErrorCode.InvalidPlacement, "Invalid placement", packet.RequestId);
            return;
        }

        _playerManager.ModifyGold(player.PlayerId, -dynamicCost);

        // Increment purchase count for this player tower
        _towerPurchaseCounts[packet.PlayerTowerId] = purchaseCount + 1;

        // Fetch player bonuses for this tower (may be async with real service)
        var bonuses = await _towerBonusService.GetBonusesAsync(packet.PlayerTowerId);
        var weaponStyle = await _towerBonusService.GetWeaponAttackStyleAsync(packet.PlayerTowerId);

        // Queue tower creation and broadcast to run on the game loop thread,
        // since the await above may resume on a threadpool thread and ENet is not thread-safe.
        _pendingActions.Enqueue(() =>
        {
            var modifiedStats = ApplyBonusesToStats(baseStats, bonuses, weaponStyle);

            var tower = new Tower
            {
                EntityId = ++_nextEntityId,
                Type = packet.TowerType,
                PlayerTowerId = packet.PlayerTowerId,
                OwnerId = player.PlayerId,
                OwnerUserId = player.UserId,
                GridX = packet.GridX,
                GridY = packet.GridY,
                X = packet.GridX * GameConstants.GridCellSize + GameConstants.GridCellSize / 2f,
                Y = packet.GridY * GameConstants.GridCellSize + GameConstants.GridCellSize / 2f,
                Health = (int)(100 + bonuses.TowerHpFlat + 100 * bonuses.TowerHpPercent / 100),
                MaxHealth = (int)(100 + bonuses.TowerHpFlat + 100 * bonuses.TowerHpPercent / 100),
                Stats = modifiedStats,
                Bonuses = bonuses,
                WeaponStyle = weaponStyle,
                CritChance = bonuses.CritChance,
                CritMultiplier = 150 + bonuses.CritMultiplier // Base 150% crit damage
            };

            lock (_entitiesLock)
            {
                _towers[tower.EntityId] = tower;
            }

            BroadcastEntitySpawn(tower);

            _eventPublisher.PublishAsync(new
            {
                EventType = "tower.built",
                MatchId = _matchId,
                PlayerId = player.UserId,
                TowerId = tower.EntityId,
                TowerType = (byte)packet.TowerType,
                packet.GridX,
                packet.GridY,
                GoldSpent = dynamicCost,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogDebug("Player {PlayerId} built tower {TowerId} at ({X}, {Y}) with {DamageBonus}% damage bonus",
                player.PlayerId, tower.EntityId, packet.GridX, packet.GridY, bonuses.DamagePercent);
        });
    }

    private TowerStats ApplyBonusesToStats(TowerStats baseStats, TowerBonusSummaryDto bonuses, WeaponAttackStyleDto? weapon)
    {
        // If weapon equipped, override attack stats
        if (weapon != null)
        {
            return new TowerStats(
                Cost: baseStats.Cost,
                Damage: (int)(weapon.Damage * (1 + bonuses.DamagePercent / 100) + bonuses.DamageFlat),
                Range: (float)(weapon.Range * (1 + bonuses.RangePercent / 100)),
                AttackSpeed: (float)(weapon.AttackSpeed * (1 + bonuses.AttackSpeedPercent / 100)),
                SellValue: baseStats.SellValue,
                DamageType: baseStats.DamageType, // Damage type determined by tower/weapon affixes
                ProjectileSpeed: weapon.IsProjectile ? 12f : 0,
                SplashRadius: baseStats.SplashRadius,
                SlowAmount: baseStats.SlowAmount,
                SlowDuration: baseStats.SlowDuration
            );
        }

        // Apply percentage bonuses
        var damageMultiplier = 1 + bonuses.DamagePercent / 100;
        var rangeMultiplier = 1 + bonuses.RangePercent / 100;
        var attackSpeedMultiplier = 1 + bonuses.AttackSpeedPercent / 100;

        return new TowerStats(
            Cost: baseStats.Cost,
            Damage: (int)(baseStats.Damage * damageMultiplier + bonuses.DamageFlat),
            Range: (float)((decimal)baseStats.Range * rangeMultiplier),
            AttackSpeed: (float)((decimal)baseStats.AttackSpeed * attackSpeedMultiplier),
            SellValue: baseStats.SellValue,
            DamageType: baseStats.DamageType,
            ProjectileSpeed: baseStats.ProjectileSpeed,
            SplashRadius: baseStats.SplashRadius,
            SlowAmount: baseStats.SlowAmount,
            SlowDuration: baseStats.SlowDuration
        );
    }

    public void ProcessTowerUpgrade(uint peerId, TowerUpgradePacket packet)
    {
        var player = _playerManager.GetPlayer(peerId);
        if (player == null) return;

        lock (_entitiesLock)
        {
            if (!_towers.TryGetValue(packet.TowerId, out var tower))
            {
                SendError(peerId, ErrorCode.TowerNotFound, "Tower not found", packet.RequestId);
                return;
            }

            if (tower.OwnerId != player.PlayerId)
            {
                SendError(peerId, ErrorCode.TowerNotFound, "Not your tower", packet.RequestId);
                return;
            }

            var upgradeCost = tower.Stats.Cost / 2;
            if (player.Gold < upgradeCost)
            {
                SendError(peerId, ErrorCode.InsufficientGold, "Not enough gold", packet.RequestId);
                return;
            }

            _playerManager.ModifyGold(player.PlayerId, -upgradeCost);
            tower.UpgradeLevel++;

            _logger.LogDebug("Player {PlayerId} upgraded tower {TowerId} to level {Level}",
                player.PlayerId, tower.EntityId, tower.UpgradeLevel);
        }
    }

    public void ProcessTowerSell(uint peerId, TowerSellPacket packet)
    {
        var player = _playerManager.GetPlayer(peerId);
        if (player == null) return;

        lock (_entitiesLock)
        {
            if (!_towers.TryGetValue(packet.TowerId, out var tower))
            {
                SendError(peerId, ErrorCode.TowerNotFound, "Tower not found", packet.RequestId);
                return;
            }

            if (tower.OwnerId != player.PlayerId)
            {
                SendError(peerId, ErrorCode.TowerNotFound, "Not your tower", packet.RequestId);
                return;
            }

            var sellValue = tower.Stats.SellValue;
            _playerManager.ModifyGold(player.PlayerId, sellValue);
            _towers.Remove(tower.EntityId);

            BroadcastEntityDestroy(tower.EntityId, DestroyReason.Sold);

            _eventPublisher.PublishAsync(new
            {
                EventType = "tower.sold",
                MatchId = _matchId,
                PlayerId = player.UserId,
                TowerId = tower.EntityId,
                GoldReceived = sellValue,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogDebug("Player {PlayerId} sold tower {TowerId} for {Gold} gold",
                player.PlayerId, tower.EntityId, sellValue);
        }
    }

    public void ProcessAbilityUse(uint peerId, AbilityUsePacket packet)
    {
        var player = _playerManager.GetPlayer(peerId);
        if (player == null) return;

        _eventPublisher.PublishAsync(new
        {
            EventType = "ability.used",
            MatchId = _matchId,
            PlayerId = player.UserId,
            AbilityType = (byte)packet.AbilityType,
            packet.TargetX,
            packet.TargetY,
            Timestamp = DateTime.UtcNow
        });

        _logger.LogDebug("Player {PlayerId} used ability {Ability} at ({X}, {Y})",
            player.PlayerId, packet.AbilityType, packet.TargetX, packet.TargetY);
    }

    public void ProcessItemCollect(uint peerId, ItemCollectPacket packet)
    {
        var player = _playerManager.GetPlayer(peerId);
        if (player == null) return;

        lock (_entitiesLock)
        {
            if (!_itemDrops.TryGetValue(packet.DropId, out var drop))
            {
                _send(peerId, new ItemCollectAckPacket
                {
                    RequestId = packet.RequestId,
                    Success = false,
                    DropId = packet.DropId,
                    ErrorMessage = "Item drop not found"
                });
                return;
            }

            if (drop.IsCollected)
            {
                _send(peerId, new ItemCollectAckPacket
                {
                    RequestId = packet.RequestId,
                    Success = false,
                    DropId = packet.DropId,
                    ErrorMessage = "Item already collected"
                });
                return;
            }

            // Verify ownership - only the owner can collect
            if (drop.OwnerId != player.PlayerId)
            {
                _send(peerId, new ItemCollectAckPacket
                {
                    RequestId = packet.RequestId,
                    Success = false,
                    DropId = packet.DropId,
                    ErrorMessage = "Cannot collect another player's item"
                });
                return;
            }

            drop.IsCollected = true;
            var itemId = Guid.NewGuid();

            // Publish event for persistence service to create the actual item
            _eventPublisher.PublishAsync(new
            {
                EventType = "item.collected",
                MatchId = _matchId,
                PlayerId = drop.OwnerUserId,
                ItemId = itemId,
                DropId = drop.DropId,
                ItemType = (byte)drop.ItemType,
                Rarity = (byte)drop.Rarity,
                ItemLevel = drop.ItemLevel,
                Name = drop.Name,
                Timestamp = DateTime.UtcNow
            });

            // Send success ACK to collecting player
            _send(peerId, new ItemCollectAckPacket
            {
                RequestId = packet.RequestId,
                Success = true,
                DropId = packet.DropId,
                ItemId = itemId
            });

            // Remove drop from tracking
            _itemDrops.Remove(packet.DropId);

            _logger.LogDebug("Player {PlayerId} collected item drop {DropId} ({Name})",
                player.PlayerId, drop.DropId, drop.Name);
        }
    }

    private void TrySpawnItemDrop(Unit unit, Tower killerTower)
    {
        // Calculate drop chance based on unit type
        var baseDropChance = unit.Type switch
        {
            UnitType.Boss => 0.5f,
            UnitType.Tank => 0.15f,
            UnitType.Fast => 0.08f,
            _ => 0.05f
        };

        // Apply unit rarity multiplier to drop chance
        var rarityDropMultiplier = unit.Rarity switch
        {
            UnitRarity.Magic => UnitModifierConstants.MagicDropChanceMultiplier,
            UnitRarity.Rare => UnitModifierConstants.RareDropChanceMultiplier,
            _ => 1f
        };

        // Item find bonus not currently in skill tree - could be added via equipment affixes later
        var itemFindBonus = 0f;
        var finalDropChance = baseDropChance * rarityDropMultiplier * (1 + itemFindBonus / 100f);

        if (_random.NextDouble() > finalDropChance)
            return;

        // Roll item rarity - magic/rare units drop better items
        var rarity = RollDropRarity(unit.Type, unit.Rarity);

        // Roll item type
        var itemType = (ItemType)_random.Next(3);

        // Generate item name based on type and rarity
        var itemName = GenerateItemName(itemType, rarity);

        // Calculate item level based on wave
        var itemLevel = Math.Max(1, _currentWave);

        var drop = new ServerItemDrop
        {
            DropId = ++_nextEntityId,
            X = unit.X,
            Y = unit.Y,
            ItemType = itemType,
            Rarity = rarity,
            ItemLevel = itemLevel,
            Name = itemName,
            OwnerId = killerTower.OwnerId,
            OwnerUserId = killerTower.OwnerUserId,
            CreatedAt = DateTime.UtcNow
        };

        lock (_entitiesLock)
        {
            _itemDrops[drop.DropId] = drop;
        }

        BroadcastItemDrop(drop);

        _logger.LogDebug("Item dropped at ({X}, {Y}): {Name} ({Rarity}) for player {PlayerId}",
            drop.X, drop.Y, drop.Name, drop.Rarity, drop.OwnerId);
    }

    private ItemRarity RollDropRarity(UnitType unitType, UnitRarity unitRarity = UnitRarity.Normal)
    {
        var roll = _random.Next(100);

        // Base thresholds
        var rareThreshold = ItemDropConstants.RareWeight;
        var magicThreshold = ItemDropConstants.RareWeight + ItemDropConstants.MagicWeight;

        // Bosses have better drop rates
        if (unitType == UnitType.Boss)
        {
            rareThreshold = 15;
            magicThreshold = 50;
        }

        // Magic/Rare units have better drop rates
        if (unitRarity == UnitRarity.Magic)
        {
            rareThreshold += 5;
            magicThreshold += 15;
        }
        else if (unitRarity == UnitRarity.Rare)
        {
            rareThreshold += 15;
            magicThreshold += 30;
        }

        if (roll < rareThreshold)
            return ItemRarity.Rare;
        if (roll < magicThreshold)
            return ItemRarity.Magic;
        return ItemRarity.Normal;
    }

    private string GenerateItemName(ItemType itemType, ItemRarity rarity)
    {
        var prefixes = rarity switch
        {
            ItemRarity.Rare => new[] { "Superior", "Exquisite", "Masterwork", "Ancient" },
            ItemRarity.Magic => new[] { "Enchanted", "Mystic", "Arcane", "Glowing" },
            _ => new[] { "Basic", "Simple", "Common", "Plain" }
        };

        var typeNames = itemType switch
        {
            ItemType.Weapon => new[] { "Sword", "Bow", "Staff", "Axe", "Dagger" },
            ItemType.Shield => new[] { "Shield", "Buckler", "Tower Shield", "Aegis" },
            _ => new[] { "Ring", "Amulet", "Charm", "Talisman" }
        };

        var prefix = prefixes[_random.Next(prefixes.Length)];
        var typeName = typeNames[_random.Next(typeNames.Length)];

        return $"{prefix} {typeName}";
    }

    private void BroadcastItemDrop(ServerItemDrop drop)
    {
        _broadcast(new ItemDropPacket
        {
            DropId = drop.DropId,
            X = drop.X,
            Y = drop.Y,
            ItemType = drop.ItemType,
            Rarity = drop.Rarity,
            ItemLevel = drop.ItemLevel,
            Name = drop.Name,
            OwnerId = drop.OwnerId
        });
    }

    public void StartMatch(GameMode mode, MapInfo map)
    {
        var players = _playerManager.GetAllPlayers();

        _broadcast(new MatchStartPacket
        {
            MatchId = _matchId,
            Mode = mode,
            Players = players.Select(p => new PlayerInfo
            {
                PlayerId = p.PlayerId,
                UserId = p.UserId,
                Name = p.Name,
                TeamId = p.TeamId,
                EloRating = GameConstants.DefaultEloRating
            }).ToArray(),
            Map = map
        });

        _state = GameState.Preparation;
        _logger.LogInformation("Match {MatchId} started with {PlayerCount} players", _matchId, players.Count);

        _eventPublisher.PublishAsync(new
        {
            EventType = "match.started",
            MatchId = _matchId,
            Mode = mode.ToString(),
            PlayerIds = players.Select(p => p.UserId).ToArray(),
            MapId = map.MapId,
            Timestamp = DateTime.UtcNow
        });
    }

    public void StartWave()
    {
        _currentWave++;
        _state = GameState.WaveActive;

        var waveInfo = GenerateWaveInfo(_currentWave);

        _broadcast(new WaveStartPacket
        {
            WaveNumber = (uint)_currentWave,
            WaveInfo = waveInfo
        });

        SpawnWaveUnits(waveInfo);

        _logger.LogInformation("Wave {Wave} started", _currentWave);
    }

    private void HandleAllPlayersReady()
    {
        if (_state == GameState.WaitingForPlayers)
        {
            var map = GenerateDefaultMap();
            StartMatch(GameMode.Solo, map);

            _waveStartDelay = 5f;
        }
        else if (_state == GameState.Preparation)
        {
            StartWave();
        }
    }

    private void UpdateUnits(float deltaTime)
    {
        lock (_entitiesLock)
        {
            var unitsToRemove = new List<uint>();

            foreach (var unit in _units.Values)
            {
                // Movement
                unit.X += unit.DirectionX * unit.Speed * deltaTime;
                unit.Y += unit.DirectionY * unit.Speed * deltaTime;

                // Regenerating modifier - heal over time
                if (unit.HasModifier(UnitModifier.Regenerating) && unit.Health < unit.MaxHealth)
                {
                    var regenAmount = (int)(unit.MaxHealth * UnitModifierConstants.RegenerationPerSecond * deltaTime);
                    unit.Health = Math.Min(unit.Health + regenAmount, unit.MaxHealth);
                }

                // Check if reached end
                if (unit.X < 0 || unit.X > GameConstants.DefaultMapWidth * GameConstants.GridCellSize)
                {
                    DamagePlayer(unit);
                    unitsToRemove.Add(unit.EntityId);
                }
            }

            foreach (var id in unitsToRemove)
            {
                _units.Remove(id);
                BroadcastEntityDestroy(id, DestroyReason.ReachedEnd);
            }
        }
    }

    private void UpdateTowers(float deltaTime)
    {
        lock (_entitiesLock)
        {
            foreach (var tower in _towers.Values)
            {
                tower.AttackCooldown -= deltaTime;
                if (tower.AttackCooldown <= 0)
                {
                    var target = FindNearestUnit(tower.X, tower.Y, tower.Stats.Range * GameConstants.GridCellSize);
                    if (target != null)
                    {
                        DamageUnit(target, tower);
                        tower.AttackCooldown = 1f / tower.Stats.AttackSpeed;
                    }
                }
            }
        }
    }

    private Unit? FindNearestUnit(float x, float y, float range)
    {
        Unit? nearest = null;
        var nearestDist = float.MaxValue;

        foreach (var unit in _units.Values)
        {
            var dx = unit.X - x;
            var dy = unit.Y - y;
            var dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist <= range && dist < nearestDist)
            {
                nearest = unit;
                nearestDist = dist;
            }
        }

        return nearest;
    }

    private void DamageUnit(Unit unit, Tower tower)
    {
        // Calculate damage with critical strike chance
        var damage = tower.Stats.Damage;
        var isCrit = false;

        if (tower.CritChance > 0 && _random.NextDouble() * 100 < (double)tower.CritChance)
        {
            damage = (int)(damage * tower.CritMultiplier / 100);
            isCrit = true;
        }

        // Handle weapon multi-hit for sword (arc sweep) and axe (return hit)
        if (tower.WeaponStyle != null)
        {
            if (tower.WeaponStyle.HitsMultiple)
            {
                // Sword arc sweep - damage nearby units too
                var nearbyUnits = FindUnitsInRange(tower.X, tower.Y, tower.Stats.Range * GameConstants.GridCellSize)
                    .Take(tower.WeaponStyle.MaxTargets)
                    .ToList();

                foreach (var nearbyUnit in nearbyUnits)
                {
                    if (nearbyUnit.EntityId != unit.EntityId)
                    {
                        ApplyDamageToUnit(nearbyUnit, damage, tower, isCrit);
                    }
                }
            }

            // Axe return hit handled by attack cooldown timing
        }

        ApplyDamageToUnit(unit, damage, tower, isCrit);
    }

    private void ApplyDamageToUnit(Unit unit, int damage, Tower tower, bool isCrit)
    {
        // Check for Shielded modifier - blocks first hit
        if (unit.ShieldActive && unit.HasModifier(UnitModifier.Shielded))
        {
            unit.ShieldActive = false;
            _logger.LogDebug("Unit {EntityId} shield absorbed hit", unit.EntityId);
            return;
        }

        // Apply resistance based on tower's damage type
        var resistance = unit.GetResistance(tower.Stats.DamageType);
        var finalDamage = (int)(damage * (1f - resistance));

        unit.Health -= finalDamage;

        if (unit.Health <= 0)
        {
            lock (_entitiesLock)
            {
                _units.Remove(unit.EntityId);
            }

            _unitsKilledThisWave++;

            var stats = UnitDefinitions.GetStats(unit.Type);

            // Calculate gold reward with rarity multiplier
            var goldReward = stats.GoldReward;
            goldReward = unit.Rarity switch
            {
                UnitRarity.Magic => (int)(goldReward * UnitModifierConstants.MagicGoldMultiplier),
                UnitRarity.Rare => (int)(goldReward * UnitModifierConstants.RareGoldMultiplier),
                _ => goldReward
            };

            // Apply gold find bonus
            if (tower.Bonuses != null && tower.Bonuses.GoldFindPercent > 0)
            {
                goldReward = (int)(goldReward * (1 + tower.Bonuses.GoldFindPercent / 100));
            }

            _playerManager.ModifyGold(tower.OwnerId, goldReward);
            _playerManager.AddScore(tower.OwnerId, stats.ScoreValue);

            // Calculate XP with rarity multiplier
            var xpAmount = TowerProgressionConstants.XpSources.UnitKill;
            xpAmount = unit.Rarity switch
            {
                UnitRarity.Magic => (int)(xpAmount * UnitModifierConstants.MagicXpMultiplier),
                UnitRarity.Rare => (int)(xpAmount * UnitModifierConstants.RareXpMultiplier),
                _ => xpAmount
            };

            if (unit.Type == UnitType.Boss)
            {
                xpAmount += TowerProgressionConstants.XpSources.BossKill;
            }

            // Apply XP gain bonus
            if (tower.Bonuses != null && tower.Bonuses.XpGainPercent > 0)
            {
                xpAmount = (int)(xpAmount * (1 + tower.Bonuses.XpGainPercent / 100));
            }

            AccumulateTowerXp(tower.PlayerTowerId, xpAmount);

            BroadcastEntityDestroy(unit.EntityId, DestroyReason.Killed);

            // Try to spawn an item drop (with rarity bonus)
            TrySpawnItemDrop(unit, tower);

            // Publish unit killed event with tower info
            _eventPublisher.PublishAsync(new
            {
                EventType = "unit.killed",
                MatchId = _matchId,
                PlayerId = tower.OwnerUserId,
                UnitId = unit.EntityId,
                UnitType = (byte)unit.Type,
                UnitRarity = (byte)unit.Rarity,
                KillerTowerId = tower.EntityId,
                GoldAwarded = goldReward,
                IsCritical = isCrit,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private void AccumulateTowerXp(Guid playerTowerId, int xpAmount)
    {
        if (_towerXpAccumulated.ContainsKey(playerTowerId))
            _towerXpAccumulated[playerTowerId] += xpAmount;
        else
            _towerXpAccumulated[playerTowerId] = xpAmount;
    }

    private IEnumerable<Unit> FindUnitsInRange(float x, float y, float range)
    {
        foreach (var unit in _units.Values)
        {
            var dx = unit.X - x;
            var dy = unit.Y - y;
            var dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist <= range)
            {
                yield return unit;
            }
        }
    }

    private void DamagePlayer(Unit unit)
    {
        var players = _playerManager.GetAllPlayers();
        if (players.Count == 0) return;

        var player = players[0];
        var stats = UnitDefinitions.GetStats(unit.Type);
        _playerManager.ModifyLives(player.PlayerId, -stats.LivesCost);

        _unitsLeakedThisWave++;

        _eventPublisher.PublishAsync(new
        {
            EventType = "player.damaged",
            MatchId = _matchId,
            PlayerId = player.UserId,
            Damage = stats.LivesCost,
            RemainingLives = player.Lives - stats.LivesCost,
            Timestamp = DateTime.UtcNow
        });

        if (player.Lives <= stats.LivesCost)
        {
            EndMatch(MatchResult.Defeat);
        }
    }

    private void CheckWaveCompletion()
    {
        lock (_entitiesLock)
        {
            if (_units.Count == 0 && _state == GameState.WaveActive)
            {
                _state = GameState.Preparation;

                var players = _playerManager.GetAllPlayers();
                var isPerfectWave = _unitsLeakedThisWave == 0;

                foreach (var player in players)
                {
                    _playerManager.ModifyGold(player.PlayerId, GameConstants.WaveCompletionBonus);

                    // Award wave clear XP to all player towers the player has placed
                    var playerTowerIds = _towers.Values
                        .Where(t => t.OwnerUserId == player.UserId)
                        .Select(t => t.PlayerTowerId)
                        .Distinct();

                    foreach (var playerTowerId in playerTowerIds)
                    {
                        var xpAmount = TowerProgressionConstants.XpSources.WaveClear;
                        if (isPerfectWave)
                        {
                            xpAmount += TowerProgressionConstants.XpSources.PerfectWave;
                        }
                        AccumulateTowerXp(playerTowerId, xpAmount);
                    }

                    // Check for item drops
                    ProcessWaveDrops(player.UserId, isPerfectWave);
                }

                // Publish accumulated XP
                PublishAccumulatedXp();

                _broadcast(new WaveEndPacket
                {
                    WaveNumber = (uint)_currentWave,
                    Success = true,
                    BonusGold = GameConstants.WaveCompletionBonus
                });

                // Publish wave completed event
                _eventPublisher.PublishAsync(new
                {
                    EventType = "wave.completed",
                    MatchId = _matchId,
                    WaveNumber = _currentWave,
                    UnitsKilled = _unitsKilledThisWave,
                    UnitsLeaked = _unitsLeakedThisWave,
                    IsPerfect = isPerfectWave,
                    Timestamp = DateTime.UtcNow
                });

                // Reset wave tracking
                _unitsKilledThisWave = 0;
                _unitsLeakedThisWave = 0;

                _logger.LogInformation("Wave {Wave} completed (perfect: {IsPerfect})", _currentWave, isPerfectWave);

                // Auto-start next wave after a short delay (for solo mode)
                if (_gameMode == GameMode.Solo)
                {
                    _waveStartDelay = 5f; // 5 seconds between waves
                }
            }
        }
    }

    private void ProcessWaveDrops(Guid userId, bool isPerfectWave)
    {
        // Calculate drop chance based on wave
        var baseChance = ItemDropConstants.DropChances.WaveCompletionBase +
            (_currentWave - 1) * ItemDropConstants.DropChances.WaveCompletionScaling;

        // Perfect wave = guaranteed drop
        if (isPerfectWave || _random.NextDouble() < baseChance)
        {
            var rarity = RollItemRarity();
            if (isPerfectWave && rarity == ItemRarity.Normal)
            {
                rarity = ItemRarity.Magic; // Upgrade to at least magic for perfect waves
            }

            PublishItemDrop(userId, rarity, isPerfectWave ? "perfect_wave" : "wave_completion");
        }
    }

    private ItemRarity RollItemRarity()
    {
        var roll = _random.Next(100);
        if (roll < ItemDropConstants.RareWeight)
            return ItemRarity.Rare;
        if (roll < ItemDropConstants.RareWeight + ItemDropConstants.MagicWeight)
            return ItemRarity.Magic;
        return ItemRarity.Normal;
    }

    private void PublishItemDrop(Guid userId, ItemRarity rarity, string source)
    {
        _eventPublisher.PublishAsync(new
        {
            EventType = "item.dropped",
            MatchId = _matchId,
            PlayerId = userId,
            ItemId = Guid.NewGuid(), // Placeholder - actual item created by consumer
            Rarity = (byte)rarity,
            ItemType = (byte)_random.Next(3), // Random item type
            Source = source,
            Timestamp = DateTime.UtcNow
        });
    }

    private void PublishAccumulatedXp()
    {
        foreach (var kvp in _towerXpAccumulated)
        {
            if (kvp.Value > 0)
            {
                _eventPublisher.PublishAsync(new
                {
                    EventType = "tower.xp_gained",
                    MatchId = _matchId,
                    TowerId = kvp.Key,
                    XpAmount = kvp.Value,
                    Source = "accumulated",
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        _towerXpAccumulated.Clear();
    }

    private void EndMatch(MatchResult result)
    {
        _state = GameState.GameOver;

        var players = _playerManager.GetAllPlayers();

        var matchStats = new MatchStats
        {
            TotalWaves = _currentWave,
            UnitsKilled = 0,
            TowersBuilt = _towers.Count,
            GoldEarned = 0,
            MatchDuration = _currentTick * GameConstants.TickInterval,
            PlayerStats = players.Select(p => new PlayerMatchStats
            {
                PlayerId = p.PlayerId,
                UnitsKilled = 0,
                TowersBuilt = 0,
                GoldEarned = 0,
                DamageDealt = 0,
                LivesLost = GameConstants.StartingLives - p.Lives
            }).ToArray()
        };

        // Award end-of-match XP and item drops
        foreach (var player in players)
        {
            var playerTowerIds = _towers.Values
                .Where(t => t.OwnerUserId == player.UserId)
                .Select(t => t.PlayerTowerId)
                .Distinct();

            foreach (var playerTowerId in playerTowerIds)
            {
                var xpAmount = TowerProgressionConstants.XpSources.MatchComplete;
                if (result == MatchResult.Victory)
                {
                    xpAmount += TowerProgressionConstants.XpSources.Victory;
                }
                AccumulateTowerXp(playerTowerId, xpAmount);
            }

            // End-of-match item drops
            ProcessMatchEndDrops(player.UserId, result);
        }

        // Publish accumulated XP
        PublishAccumulatedXp();

        _broadcast(new MatchEndPacket
        {
            MatchId = _matchId,
            Result = result,
            Stats = matchStats
        });

        _eventPublisher.PublishAsync(new
        {
            EventType = "match.ended",
            MatchId = _matchId,
            Result = result.ToString(),
            WavesCompleted = _currentWave,
            DurationSeconds = matchStats.MatchDuration,
            Timestamp = DateTime.UtcNow
        });

        _logger.LogInformation("Match {MatchId} ended: {Result}, waves: {Waves}", _matchId, result, _currentWave);

        OnSessionEnded?.Invoke(this);
    }

    public void ForceEnd()
    {
        if (_state != GameState.GameOver)
        {
            _state = GameState.GameOver;
            _logger.LogInformation("Session {MatchId} force-ended", _matchId);
            OnSessionEnded?.Invoke(this);
        }
    }

    private void ProcessMatchEndDrops(Guid userId, MatchResult result)
    {
        var totalDrops = ItemDropConstants.MatchRewards.CompletionItems;

        if (result == MatchResult.Victory)
        {
            totalDrops += ItemDropConstants.MatchRewards.VictoryBonusItems;
        }

        // Wave milestone bonuses
        if (_currentWave >= 10)
            totalDrops += ItemDropConstants.MatchRewards.Wave10Milestone;
        if (_currentWave >= 20)
            totalDrops += ItemDropConstants.MatchRewards.Wave20Milestone;
        if (_currentWave >= 30)
            totalDrops += ItemDropConstants.MatchRewards.Wave30Milestone;

        for (int i = 0; i < totalDrops; i++)
        {
            var rarity = RollItemRarity();
            // Victory bonus: upgrade one drop to at least Magic
            if (result == MatchResult.Victory && i == 0 && rarity == ItemRarity.Normal)
            {
                rarity = ItemRarity.Magic;
            }
            PublishItemDrop(userId, rarity, "match_reward");
        }
    }

    private void SpawnWaveUnits(WaveInfo waveInfo)
    {
        foreach (var spawnInfo in waveInfo.Units)
        {
            for (var i = 0; i < spawnInfo.Count; i++)
            {
                var stats = UnitDefinitions.ScaleForWave(spawnInfo.Type, _currentWave);

                // Roll rarity
                var (rarity, modifiers) = RollUnitRarity();

                // Calculate modified stats
                var baseHealth = stats.Health;
                var baseSpeed = stats.Speed * GameConstants.GridCellSize;

                var healthMultiplier = 1f;
                var speedMultiplier = 1f;

                // Apply modifier stat bonuses
                if ((modifiers & UnitModifier.Tough) != 0)
                    healthMultiplier += UnitModifierConstants.ToughHealthBonus;
                if ((modifiers & UnitModifier.Armored) != 0)
                    healthMultiplier += UnitModifierConstants.ArmoredHealthBonus;
                if ((modifiers & UnitModifier.Swift) != 0)
                    speedMultiplier += UnitModifierConstants.SwiftSpeedBonus;
                if ((modifiers & UnitModifier.Hasted) != 0)
                    speedMultiplier += UnitModifierConstants.HasteSpeedBonus;

                var finalHealth = (int)(baseHealth * healthMultiplier);
                var finalSpeed = baseSpeed * speedMultiplier;

                var unit = new Unit
                {
                    EntityId = ++_nextEntityId,
                    Type = spawnInfo.Type,
                    X = 0,
                    Y = GameConstants.GridCellSize * 5 + i * 20,
                    DirectionX = 1,
                    DirectionY = 0,
                    Health = finalHealth,
                    MaxHealth = finalHealth,
                    Speed = finalSpeed,
                    BaseSpeed = finalSpeed,
                    Rarity = rarity,
                    Modifiers = modifiers,
                    ShieldActive = (modifiers & UnitModifier.Shielded) != 0
                };

                lock (_entitiesLock)
                {
                    _units[unit.EntityId] = unit;
                }

                BroadcastEntitySpawn(unit);

                if (rarity != UnitRarity.Normal)
                {
                    _logger.LogDebug("Spawned {Rarity} unit {EntityId} with modifiers: {Modifiers}",
                        rarity, unit.EntityId, modifiers);
                }
            }
        }
    }

    private (UnitRarity rarity, UnitModifier modifiers) RollUnitRarity()
    {
        var roll = _random.Next(100);

        UnitRarity rarity;
        int modifierCount;

        if (roll < UnitModifierConstants.RareChance)
        {
            rarity = UnitRarity.Rare;
            modifierCount = _random.Next(
                UnitModifierConstants.RareModifierMin,
                UnitModifierConstants.RareModifierMax + 1);
        }
        else if (roll < UnitModifierConstants.RareChance + UnitModifierConstants.MagicChance)
        {
            rarity = UnitRarity.Magic;
            modifierCount = _random.Next(
                UnitModifierConstants.MagicModifierMin,
                UnitModifierConstants.MagicModifierMax + 1);
        }
        else
        {
            return (UnitRarity.Normal, UnitModifier.None);
        }

        // Roll modifiers
        var modifiers = UnitModifier.None;
        var availableModifiers = UnitModifierConstants.AllModifiers.ToList();

        for (int i = 0; i < modifierCount && availableModifiers.Count > 0; i++)
        {
            var index = _random.Next(availableModifiers.Count);
            modifiers |= availableModifiers[index];
            availableModifiers.RemoveAt(index);
        }

        return (rarity, modifiers);
    }

    private WaveInfo GenerateWaveInfo(int waveNumber)
    {
        var unitCount = 5 + waveNumber * 2;
        var unitType = waveNumber switch
        {
            <= 3 => UnitType.Basic,
            <= 6 => UnitType.Fast,
            <= 10 => UnitType.Tank,
            _ => UnitType.Boss
        };

        return new WaveInfo
        {
            Units = [new UnitSpawnInfo { Type = unitType, Count = unitCount, PathIndex = 0 }],
            SpawnInterval = 0.5f,
            TotalUnits = unitCount
        };
    }

    private MapInfo GenerateDefaultMap()
    {
        return new MapInfo
        {
            MapId = "default",
            Width = GameConstants.DefaultMapWidth,
            Height = GameConstants.DefaultMapHeight,
            Paths =
            [
                [
                    new PathPoint { X = 0, Y = 5 },
                    new PathPoint { X = GameConstants.DefaultMapWidth, Y = 5 }
                ]
            ],
            BuildableZones =
            [
                new BuildableZone
                {
                    X = 0,
                    Y = 0,
                    Width = GameConstants.DefaultMapWidth,
                    Height = GameConstants.DefaultMapHeight,
                    TeamId = 0
                }
            ]
        };
    }

    private bool IsValidPlacement(int gridX, int gridY, uint playerId)
    {
        if (gridX < 0 || gridX >= GameConstants.DefaultMapWidth) return false;
        if (gridY < 0 || gridY >= GameConstants.DefaultMapHeight) return false;
        if (gridY == 5) return false;

        lock (_entitiesLock)
        {
            return !_towers.Values.Any(t => t.GridX == gridX && t.GridY == gridY);
        }
    }

    private void BroadcastEntitySpawn(Tower tower)
    {
        _broadcast(new EntitySpawnPacket
        {
            Tick = _currentTick,
            Entity = new EntityState
            {
                EntityId = tower.EntityId,
                Type = EntityType.Tower,
                X = tower.X,
                Y = tower.Y,
                Rotation = 0,
                Health = tower.Health,
                MaxHealth = tower.MaxHealth,
                OwnerId = tower.OwnerId
            }
        });
    }

    private void BroadcastEntitySpawn(Unit unit)
    {
        // Pack unit rarity and modifiers into ExtraData
        // Format: [0] = UnitType, [1] = Rarity, [2-5] = Modifiers (uint)
        var extraData = new byte[6];
        extraData[0] = (byte)unit.Type;
        extraData[1] = (byte)unit.Rarity;
        var modifierBytes = BitConverter.GetBytes((uint)unit.Modifiers);
        Array.Copy(modifierBytes, 0, extraData, 2, 4);

        _broadcast(new EntitySpawnPacket
        {
            Tick = _currentTick,
            Entity = new EntityState
            {
                EntityId = unit.EntityId,
                Type = EntityType.Unit,
                X = unit.X,
                Y = unit.Y,
                Rotation = 0,
                Health = unit.Health,
                MaxHealth = unit.MaxHealth,
                ExtraData = extraData
            }
        });
    }

    private void BroadcastEntityDestroy(uint entityId, DestroyReason reason)
    {
        _broadcast(new EntityDestroyPacket
        {
            Tick = _currentTick,
            EntityId = entityId,
            Reason = reason
        });
    }

    private void BroadcastEntityUpdates()
    {
        var deltas = new List<EntityDelta>();

        lock (_entitiesLock)
        {
            foreach (var unit in _units.Values)
            {
                deltas.Add(new EntityDelta
                {
                    EntityId = unit.EntityId,
                    Flags = DeltaFlags.Position | DeltaFlags.Health,
                    X = unit.X,
                    Y = unit.Y,
                    Health = unit.Health
                });
            }
        }

        if (deltas.Count > 0)
        {
            _broadcast(new EntityUpdatePacket
            {
                Tick = _currentTick,
                Deltas = deltas.ToArray()
            });
        }
    }

    private void SendError(uint peerId, ErrorCode code, string message, uint? requestId = null)
    {
        _send(peerId, new ErrorPacket
        {
            Code = code,
            Message = message,
            RequestId = requestId
        });
    }
}

public enum GameState
{
    WaitingForPlayers,
    Preparation,
    WaveActive,
    GameOver
}

public sealed class Tower
{
    public uint EntityId { get; init; }
    public TowerType Type { get; init; }
    public Guid PlayerTowerId { get; init; }
    public uint OwnerId { get; init; }
    public Guid OwnerUserId { get; init; }
    public int GridX { get; init; }
    public int GridY { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public int Health { get; set; }
    public int MaxHealth { get; init; }
    public int UpgradeLevel { get; set; }
    public float AttackCooldown { get; set; }
    public TowerStats Stats { get; init; }
    public TowerBonusSummaryDto? Bonuses { get; init; }
    public WeaponAttackStyleDto? WeaponStyle { get; init; }
    public decimal CritChance { get; init; }
    public decimal CritMultiplier { get; init; }
}

public sealed class Unit
{
    public uint EntityId { get; init; }
    public UnitType Type { get; init; }
    public float X { get; set; }
    public float Y { get; set; }
    public float DirectionX { get; set; }
    public float DirectionY { get; set; }
    public float Speed { get; set; }
    public float BaseSpeed { get; init; }
    public int Health { get; set; }
    public int MaxHealth { get; init; }

    // Rarity system
    public UnitRarity Rarity { get; init; } = UnitRarity.Normal;
    public UnitModifier Modifiers { get; init; } = UnitModifier.None;
    public bool ShieldActive { get; set; } = true; // For Shielded modifier

    /// <summary>
    /// Check if unit has a specific modifier
    /// </summary>
    public bool HasModifier(UnitModifier modifier) => (Modifiers & modifier) != 0;

    /// <summary>
    /// Get damage resistance for a specific damage type
    /// </summary>
    public float GetResistance(DamageType damageType)
    {
        float resistance = 0f;

        // Armored gives resistance to all damage
        if (HasModifier(UnitModifier.Armored))
            resistance += UnitModifierConstants.ArmoredResistanceAmount;

        // Specific resistances
        var resistMod = damageType switch
        {
            DamageType.Physical => UnitModifier.PhysicalResistance,
            DamageType.Fire => UnitModifier.FireResistance,
            DamageType.Cold => UnitModifier.ColdResistance,
            DamageType.Lightning => UnitModifier.LightningResistance,
            DamageType.Chaos => UnitModifier.PoisonResistance, // Chaos uses poison resistance
            _ => UnitModifier.None
        };

        if (resistMod != UnitModifier.None && HasModifier(resistMod))
            resistance += UnitModifierConstants.ElementalResistanceAmount;

        return Math.Min(resistance, 0.75f); // Cap at 75% resistance
    }
}

public sealed class ServerItemDrop
{
    public uint DropId { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public ItemType ItemType { get; init; }
    public ItemRarity Rarity { get; init; }
    public int ItemLevel { get; init; }
    public string Name { get; init; } = string.Empty;
    public uint OwnerId { get; init; }
    public Guid OwnerUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsCollected { get; set; }
}

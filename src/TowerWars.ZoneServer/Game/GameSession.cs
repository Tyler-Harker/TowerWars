using Microsoft.Extensions.Logging;
using TowerWars.Shared.Constants;
using TowerWars.Shared.DTOs;
using TowerWars.Shared.Protocol;
using TowerWars.ZoneServer.Networking;
using TowerWars.ZoneServer.Services;

namespace TowerWars.ZoneServer.Game;

public sealed class GameSession
{
    private readonly ILogger<GameSession> _logger;
    private readonly ENetServer _server;
    private readonly PlayerManager _playerManager;
    private readonly IEventPublisher _eventPublisher;
    private readonly ITowerBonusService _towerBonusService;
    private readonly Random _random = new();

    private readonly Dictionary<uint, Tower> _towers = new();
    private readonly Dictionary<uint, Unit> _units = new();
    private readonly object _entitiesLock = new();

    // Track XP per tower type per player
    private readonly Dictionary<(Guid PlayerId, TowerType TowerType), int> _towerXpAccumulated = new();
    // Track kills this wave for perfect wave bonus
    private int _unitsKilledThisWave;
    private int _unitsLeakedThisWave;

    private uint _nextEntityId;
    private uint _currentTick;
    private int _currentWave;
    private GameState _state = GameState.WaitingForPlayers;
    private readonly Guid _matchId = Guid.NewGuid();

    public Guid MatchId => _matchId;
    public GameState State => _state;
    public int CurrentWave => _currentWave;

    public GameSession(
        ILogger<GameSession> logger,
        ENetServer server,
        PlayerManager playerManager,
        IEventPublisher eventPublisher,
        ITowerBonusService towerBonusService)
    {
        _logger = logger;
        _server = server;
        _playerManager = playerManager;
        _eventPublisher = eventPublisher;
        _towerBonusService = towerBonusService;

        _playerManager.OnAllPlayersReady += HandleAllPlayersReady;
    }

    public void Update(float deltaTime)
    {
        _currentTick++;

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
        if (_state == GameState.WaveActive && _currentTick % 3 == 0)
        {
            BroadcastEntityUpdates();
        }
    }

    public void ProcessInput(uint peerId, PlayerInputPacket packet)
    {
        var player = _playerManager.GetPlayer(peerId);
        if (player == null) return;

        player.LastProcessedInputSequence = packet.InputSequence;

        _server.Send(peerId, new PlayerInputAckPacket
        {
            LastProcessedSequence = packet.InputSequence
        });
    }

    public async void ProcessTowerBuild(uint peerId, TowerBuildPacket packet)
    {
        var player = _playerManager.GetPlayer(peerId);
        if (player == null) return;

        var baseStats = TowerDefinitions.GetStats(packet.TowerType);

        if (player.Gold < baseStats.Cost)
        {
            SendError(peerId, ErrorCode.InsufficientGold, "Not enough gold", packet.RequestId);
            return;
        }

        if (!IsValidPlacement(packet.GridX, packet.GridY, player.PlayerId))
        {
            SendError(peerId, ErrorCode.InvalidPlacement, "Invalid placement", packet.RequestId);
            return;
        }

        _playerManager.ModifyGold(player.PlayerId, -baseStats.Cost);

        // Fetch player bonuses for this tower type
        var bonuses = await _towerBonusService.GetBonusesAsync(player.UserId, packet.TowerType);
        var weaponStyle = await _towerBonusService.GetWeaponAttackStyleAsync(player.UserId, packet.TowerType);

        // Apply bonuses to stats
        var modifiedStats = ApplyBonusesToStats(baseStats, bonuses, weaponStyle);

        var tower = new Tower
        {
            EntityId = ++_nextEntityId,
            Type = packet.TowerType,
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
            GoldSpent = baseStats.Cost,
            Timestamp = DateTime.UtcNow
        });

        _logger.LogDebug("Player {PlayerId} built tower {TowerId} at ({X}, {Y}) with {DamageBonus}% damage bonus",
            player.PlayerId, tower.EntityId, packet.GridX, packet.GridY, bonuses.DamagePercent);
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
                DamageType: weapon.Subtype == WeaponSubtype.Wand ? DamageType.Magic : baseStats.DamageType,
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

    public void StartMatch(GameMode mode, MapInfo map)
    {
        var players = _playerManager.GetAllPlayers();

        _server.Broadcast(new MatchStartPacket
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

        _server.Broadcast(new WaveStartPacket
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

            Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ => StartWave());
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
                unit.X += unit.DirectionX * unit.Speed * deltaTime;
                unit.Y += unit.DirectionY * unit.Speed * deltaTime;

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
        unit.Health -= damage;

        if (unit.Health <= 0)
        {
            lock (_entitiesLock)
            {
                _units.Remove(unit.EntityId);
            }

            _unitsKilledThisWave++;

            var stats = UnitDefinitions.GetStats(unit.Type);

            // Apply gold find bonus
            var goldReward = stats.GoldReward;
            if (tower.Bonuses != null && tower.Bonuses.GoldFindPercent > 0)
            {
                goldReward = (int)(goldReward * (1 + tower.Bonuses.GoldFindPercent / 100));
            }

            _playerManager.ModifyGold(tower.OwnerId, goldReward);
            _playerManager.AddScore(tower.OwnerId, stats.ScoreValue);

            // Accumulate XP for the tower type
            var xpAmount = TowerProgressionConstants.XpSources.UnitKill;
            if (unit.Type == UnitType.Boss)
            {
                xpAmount += TowerProgressionConstants.XpSources.BossKill;
            }

            // Apply XP gain bonus
            if (tower.Bonuses != null && tower.Bonuses.XpGainPercent > 0)
            {
                xpAmount = (int)(xpAmount * (1 + tower.Bonuses.XpGainPercent / 100));
            }

            AccumulateTowerXp(tower.OwnerUserId, tower.Type, xpAmount);

            BroadcastEntityDestroy(unit.EntityId, DestroyReason.Killed);

            // Publish unit killed event with tower info
            _eventPublisher.PublishAsync(new
            {
                EventType = "unit.killed",
                MatchId = _matchId,
                PlayerId = tower.OwnerUserId,
                UnitId = unit.EntityId,
                UnitType = (byte)unit.Type,
                KillerTowerId = tower.EntityId,
                GoldAwarded = goldReward,
                IsCritical = isCrit,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private void AccumulateTowerXp(Guid userId, TowerType towerType, int xpAmount)
    {
        var key = (userId, towerType);
        if (_towerXpAccumulated.ContainsKey(key))
            _towerXpAccumulated[key] += xpAmount;
        else
            _towerXpAccumulated[key] = xpAmount;
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

                    // Award wave clear XP to all tower types the player has
                    var playerTowerTypes = _towers.Values
                        .Where(t => t.OwnerUserId == player.UserId)
                        .Select(t => t.Type)
                        .Distinct();

                    foreach (var towerType in playerTowerTypes)
                    {
                        var xpAmount = TowerProgressionConstants.XpSources.WaveClear;
                        if (isPerfectWave)
                        {
                            xpAmount += TowerProgressionConstants.XpSources.PerfectWave;
                        }
                        AccumulateTowerXp(player.UserId, towerType, xpAmount);
                    }

                    // Check for item drops
                    ProcessWaveDrops(player.UserId, isPerfectWave);
                }

                // Publish accumulated XP
                PublishAccumulatedXp();

                _server.Broadcast(new WaveEndPacket
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
                    PlayerId = kvp.Key.PlayerId,
                    TowerType = (byte)kvp.Key.TowerType,
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
            var playerTowerTypes = _towers.Values
                .Where(t => t.OwnerUserId == player.UserId)
                .Select(t => t.Type)
                .Distinct();

            foreach (var towerType in playerTowerTypes)
            {
                var xpAmount = TowerProgressionConstants.XpSources.MatchComplete;
                if (result == MatchResult.Victory)
                {
                    xpAmount += TowerProgressionConstants.XpSources.Victory;
                }
                AccumulateTowerXp(player.UserId, towerType, xpAmount);
            }

            // End-of-match item drops
            ProcessMatchEndDrops(player.UserId, result);
        }

        // Publish accumulated XP
        PublishAccumulatedXp();

        _server.Broadcast(new MatchEndPacket
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
                var unit = new Unit
                {
                    EntityId = ++_nextEntityId,
                    Type = spawnInfo.Type,
                    X = 0,
                    Y = GameConstants.GridCellSize * 5 + i * 20,
                    DirectionX = 1,
                    DirectionY = 0,
                    Health = stats.Health,
                    MaxHealth = stats.Health,
                    Speed = stats.Speed * GameConstants.GridCellSize
                };

                lock (_entitiesLock)
                {
                    _units[unit.EntityId] = unit;
                }

                BroadcastEntitySpawn(unit);
            }
        }
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
        _server.Broadcast(new EntitySpawnPacket
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
        _server.Broadcast(new EntitySpawnPacket
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
                MaxHealth = unit.MaxHealth
            }
        });
    }

    private void BroadcastEntityDestroy(uint entityId, DestroyReason reason)
    {
        _server.Broadcast(new EntityDestroyPacket
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
            _server.Broadcast(new EntityUpdatePacket
            {
                Tick = _currentTick,
                Deltas = deltas.ToArray()
            }, ENet.PacketFlags.None);
        }
    }

    private void SendError(uint peerId, ErrorCode code, string message, uint? requestId = null)
    {
        _server.Send(peerId, new ErrorPacket
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
    public float Speed { get; init; }
    public int Health { get; set; }
    public int MaxHealth { get; init; }
}

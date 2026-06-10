#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace TowerDefense.Data.Config
{
    public class DataGenerator : EditorWindow
    {
        private GameConfig _gameConfig;
        private string _outputPath = "Assets/Resources/Data/Generated/";

        [MenuItem("Tools/TowerDefense/Generate Data Files")]
        public static void ShowWindow()
        {
            GetWindow<DataGenerator>("Data Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Game Data Generator", EditorStyles.boldLabel);
            GUILayout.Space(10);

            _gameConfig = (GameConfig)EditorGUILayout.ObjectField(
                "Game Config", _gameConfig, typeof(GameConfig), false
            );

            _outputPath = EditorGUILayout.TextField("Output Path", _outputPath);

            GUILayout.Space(20);

            if (GUILayout.Button("Generate All Tower Data"))
            {
                GenerateTowerData();
            }

            if (GUILayout.Button("Generate All Enemy Data"))
            {
                GenerateEnemyData();
            }

            if (GUILayout.Button("Generate All Wave Data"))
            {
                GenerateWaveData();
            }

            if (GUILayout.Button("Generate All Level Data"))
            {
                GenerateLevelData();
            }

            if (GUILayout.Button("Generate Everything"))
            {
                GenerateAll();
            }

            GUILayout.Space(20);

            if (GUILayout.Button("Create Default Config"))
            {
                CreateDefaultConfig();
            }
        }

        private void CreateDefaultConfig()
        {
            GameConfig config = CreateInstance<GameConfig>();
            
            AddDefaultTowers(config);
            AddDefaultEnemies(config);
            AddDefaultWaves(config);
            AddDefaultLevels(config);
            AddDefaultHeroes(config);

            string path = "Assets/Resources/Data/GameConfig.asset";
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();

            _gameConfig = config;
            EditorUtility.DisplayDialog("Success", "Default config created!", "OK");
        }

        private void AddDefaultTowers(GameConfig config)
        {
            config.towers.Add(new GameConfig.TowerConfig
            {
                towerName = "箭塔",
                towerType = TowerType.Archer,
                levels = new System.Collections.Generic.List<GameConfig.TowerConfig.TowerLevel>
                {
                    new GameConfig.TowerConfig.TowerLevel
                    {
                        level = 1, cost = 100, upgradeCost = 80, sellValue = 50,
                        damage = 20, attackRange = 5, attackSpeed = 2,
                        projectileType = ProjectileType.Arrow, projectileCount = 1
                    },
                    new GameConfig.TowerConfig.TowerLevel
                    {
                        level = 2, cost = 180, upgradeCost = 120, sellValue = 90,
                        damage = 35, attackRange = 5.5f, attackSpeed = 2.5f,
                        projectileType = ProjectileType.Arrow, projectileCount = 1
                    },
                    new GameConfig.TowerConfig.TowerLevel
                    {
                        level = 3, cost = 300, upgradeCost = 200, sellValue = 150,
                        damage = 50, attackRange = 6, attackSpeed = 3,
                        projectileType = ProjectileType.Arrow, projectileCount = 2
                    }
                }
            });

            config.towers.Add(new GameConfig.TowerConfig
            {
                towerName = "法师塔",
                towerType = TowerType.Mage,
                levels = new System.Collections.Generic.List<GameConfig.TowerConfig.TowerLevel>
                {
                    new GameConfig.TowerConfig.TowerLevel
                    {
                        level = 1, cost = 150, upgradeCost = 100, sellValue = 75,
                        damage = 40, attackRange = 4, attackSpeed = 1.2f,
                        projectileType = ProjectileType.Magic, slowAmount = 0.3f, slowDuration = 2
                    },
                    new GameConfig.TowerConfig.TowerLevel
                    {
                        level = 2, cost = 250, upgradeCost = 150, sellValue = 125,
                        damage = 65, attackRange = 4.5f, attackSpeed = 1.5f,
                        projectileType = ProjectileType.Magic, slowAmount = 0.4f, slowDuration = 2.5f
                    },
                    new GameConfig.TowerConfig.TowerLevel
                    {
                        level = 3, cost = 400, upgradeCost = 150, sellValue = 200,
                        damage = 100, attackRange = 5, attackSpeed = 2,
                        projectileType = ProjectileType.Magic, slowAmount = 0.5f, slowDuration = 3
                    }
                }
            });

            config.towers.Add(new GameConfig.TowerConfig
            {
                towerName = "炮塔",
                towerType = TowerType.Artillery,
                levels = new System.Collections.Generic.List<GameConfig.TowerConfig.TowerLevel>
                {
                    new GameConfig.TowerConfig.TowerLevel
                    {
                        level = 1, cost = 200, upgradeCost = 150, sellValue = 100,
                        damage = 40, attackRange = 3.5f, attackSpeed = 0.6f,
                        projectileType = ProjectileType.Cannonball, splashRadius = 1.2f, maxSplashTargets = 3
                    },
                    new GameConfig.TowerConfig.TowerLevel
                    {
                        level = 2, cost = 350, upgradeCost = 200, sellValue = 175,
                        damage = 65, attackRange = 4, attackSpeed = 0.8f,
                        projectileType = ProjectileType.Cannonball, splashRadius = 1.5f, maxSplashTargets = 4
                    },
                    new GameConfig.TowerConfig.TowerLevel
                    {
                        level = 3, cost = 550, upgradeCost = 180, sellValue = 275,
                        damage = 100, attackRange = 4.5f, attackSpeed = 1f,
                        projectileType = ProjectileType.Cannonball, splashRadius = 2f, maxSplashTargets = 5
                    }
                }
            });
        }

        private void AddDefaultEnemies(GameConfig config)
        {
            config.enemies.Add(new GameConfig.EnemyConfig
            {
                enemyName = "哥布林",
                enemyType = EnemyType.Goblin,
                stats = new GameConfig.EnemyConfig.EnemyStats
                {
                    maxHealth = 50, moveSpeed = 2.5f, goldReward = 15, lifeDamage = 1,
                    physicalResistance = 0.1f, magicResistance = 0, explosionResistance = 0,
                    isFlying = false, isArmored = false, isMagicImmune = false,
                    canBeSlowed = true, slowImmunity = 0, isBoss = false
                }
            });

            config.enemies.Add(new GameConfig.EnemyConfig
            {
                enemyName = "兽人",
                enemyType = EnemyType.Orc,
                stats = new GameConfig.EnemyConfig.EnemyStats
                {
                    maxHealth = 100, moveSpeed = 1.5f, goldReward = 25, lifeDamage = 2,
                    physicalResistance = 0.2f, magicResistance = 0.1f, explosionResistance = 0.1f,
                    isFlying = false, isArmored = true, isMagicImmune = false,
                    canBeSlowed = true, slowImmunity = 0.2f, isBoss = false
                }
            });

            config.enemies.Add(new GameConfig.EnemyConfig
            {
                enemyName = "巨魔",
                enemyType = EnemyType.Troll,
                stats = new GameConfig.EnemyConfig.EnemyStats
                {
                    maxHealth = 200, moveSpeed = 1, goldReward = 50, lifeDamage = 3,
                    physicalResistance = 0.3f, magicResistance = 0.15f, explosionResistance = 0.2f,
                    isFlying = false, isArmored = true, isMagicImmune = false,
                    canBeSlowed = true, slowImmunity = 0.3f, isBoss = false
                }
            });

            config.enemies.Add(new GameConfig.EnemyConfig
            {
                enemyName = "恶魔蝙蝠",
                enemyType = EnemyType.Flying,
                stats = new GameConfig.EnemyConfig.EnemyStats
                {
                    maxHealth = 40, moveSpeed = 3.5f, goldReward = 20, lifeDamage = 1,
                    physicalResistance = 0, magicResistance = 0.3f, explosionResistance = 0,
                    isFlying = true, isArmored = false, isMagicImmune = false,
                    canBeSlowed = true, slowImmunity = 0, isBoss = false
                }
            });

            config.enemies.Add(new GameConfig.EnemyConfig
            {
                enemyName = "黑暗骑士",
                enemyType = EnemyType.Boss,
                stats = new GameConfig.EnemyConfig.EnemyStats
                {
                    maxHealth = 1000, moveSpeed = 0.8f, goldReward = 200, lifeDamage = 5,
                    physicalResistance = 0.4f, magicResistance = 0.3f, explosionResistance = 0.2f,
                    isFlying = false, isArmored = true, isMagicImmune = false,
                    canBeSlowed = true, slowImmunity = 0.5f, isBoss = true, bossHealthMultiplier = 2
                }
            });
        }

        private void AddDefaultWaves(GameConfig config)
        {
            config.waves.Add(new GameConfig.WaveConfig
            {
                waveNumber = 1, waveDelay = 5, spawnInterval = 1.5f,
                enemies = new System.Collections.Generic.List<GameConfig.WaveConfig.WaveEnemy>
                {
                    new GameConfig.WaveConfig.WaveEnemy { enemyName = "哥布林", count = 5, spawnDelay = 0, spawnPoint = "Main" }
                }
            });

            config.waves.Add(new GameConfig.WaveConfig
            {
                waveNumber = 2, waveDelay = 5, spawnInterval = 1.2f,
                enemies = new System.Collections.Generic.List<GameConfig.WaveConfig.WaveEnemy>
                {
                    new GameConfig.WaveConfig.WaveEnemy { enemyName = "哥布林", count = 8, spawnDelay = 0, spawnPoint = "Main" },
                    new GameConfig.WaveConfig.WaveEnemy { enemyName = "兽人", count = 2, spawnDelay = 0.5f, spawnPoint = "Main" }
                }
            });

            config.waves.Add(new GameConfig.WaveConfig
            {
                waveNumber = 3, waveDelay = 5, spawnInterval = 1f,
                enemies = new System.Collections.Generic.List<GameConfig.WaveConfig.WaveEnemy>
                {
                    new GameConfig.WaveConfig.WaveEnemy { enemyName = "哥布林", count = 6, spawnDelay = 0, spawnPoint = "Main" },
                    new GameConfig.WaveConfig.WaveEnemy { enemyName = "兽人", count = 4, spawnDelay = 0.3f, spawnPoint = "Main" },
                    new GameConfig.WaveConfig.WaveEnemy { enemyName = "恶魔蝙蝠", count = 3, spawnDelay = 0.5f, spawnPoint = "Main" }
                }
            });

            config.waves.Add(new GameConfig.WaveConfig
            {
                waveNumber = 4, waveDelay = 5, spawnInterval = 0.8f,
                enemies = new System.Collections.Generic.List<GameConfig.WaveConfig.WaveEnemy>
                {
                    new GameConfig.WaveConfig.WaveEnemy { enemyName = "兽人", count = 6, spawnDelay = 0, spawnPoint = "Main" },
                    new GameConfig.WaveConfig.WaveEnemy { enemyName = "巨魔", count = 2, spawnDelay = 0.5f, spawnPoint = "Main" }
                }
            });

            config.waves.Add(new GameConfig.WaveConfig
            {
                waveNumber = 5, waveDelay = 8, spawnInterval = 0.5f,
                enemies = new System.Collections.Generic.List<GameConfig.WaveConfig.WaveEnemy>
                {
                    new GameConfig.WaveConfig.WaveEnemy { enemyName = "黑暗骑士", count = 1, spawnDelay = 0, spawnPoint = "Main" },
                    new GameConfig.WaveConfig.WaveEnemy { enemyName = "兽人", count = 5, spawnDelay = 1f, spawnPoint = "Main" }
                }
            });
        }

        private void AddDefaultLevels(GameConfig config)
        {
            config.levels.Add(new GameConfig.LevelConfig
            {
                levelName = "新手村", sceneName = "Level_01",
                initialGold = 100, initialLife = 20, timeBetweenWaves = 10,
                waveIndices = new System.Collections.Generic.List<int> { 0, 1, 2, 3, 4 },
                towerSlots = new System.Collections.Generic.List<Vector3>
                {
                    new Vector3(-3, 0, 0), new Vector3(-1, 2, 0),
                    new Vector3(1, 2, 0), new Vector3(3, 0, 0),
                    new Vector3(-2, -2, 0), new Vector3(2, -2, 0)
                },
                paths = new System.Collections.Generic.List<GameConfig.LevelConfig.PathConfig>
                {
                    new GameConfig.LevelConfig.PathConfig
                    {
                        pathName = "Main",
                        waypoints = new System.Collections.Generic.List<Vector3>
                        {
                            new Vector3(-5, 0, 0), new Vector3(-3, 0, 0),
                            new Vector3(-1, 0, 0), new Vector3(-1, 3, 0),
                            new Vector3(1, 3, 0), new Vector3(1, 0, 0),
                            new Vector3(3, 0, 0), new Vector3(5, 0, 0)
                        }
                    }
                }
            });
        }

        private void AddDefaultHeroes(GameConfig config)
        {
            config.heroes.Add(new GameConfig.HeroConfig
            {
                heroName = "圣骑士",
                stats = new GameConfig.HeroConfig.HeroStats
                {
                    maxHealth = 200, attackDamage = 30, attackRange = 1.5f,
                    attackSpeed = 1.2f, moveSpeed = 2, healthRegen = 1, armor = 10
                },
                skills = new System.Collections.Generic.List<GameConfig.HeroConfig.HeroSkill>
                {
                    new GameConfig.HeroConfig.HeroSkill
                    {
                        skillName = "神圣打击", description = "对范围内敌人造成神圣伤害",
                        cooldown = 8, manaCost = 0, effectRadius = 2, effectValue = 50
                    },
                    new GameConfig.HeroConfig.HeroSkill
                    {
                        skillName = "治愈之光", description = "恢复自身生命值",
                        cooldown = 15, manaCost = 0, effectRadius = 0, effectValue = 50
                    }
                }
            });
        }

        private void GenerateTowerData()
        {
            if (_gameConfig == null) return;

            EnsureDirectoryExists();

            foreach (var towerConfig in _gameConfig.towers)
            {
                TowerData towerData = CreateInstance<TowerData>();
                towerData.towerName = towerConfig.towerName;
                towerData.towerType = towerConfig.towerType;

                towerData.levels = new TowerData.TowerStats[towerConfig.levels.Count];
                for (int i = 0; i < towerConfig.levels.Count; i++)
                {
                    var level = towerConfig.levels[i];
                    towerData.levels[i] = new TowerData.TowerStats
                    {
                        level = level.level, cost = level.cost,
                        upgradeCost = level.upgradeCost, sellValue = level.sellValue,
                        damage = level.damage, attackRange = level.attackRange,
                        attackSpeed = level.attackSpeed, projectileType = level.projectileType,
                        projectileCount = level.projectileCount, splashRadius = level.splashRadius,
                        slowAmount = level.slowAmount, slowDuration = level.slowDuration,
                        maxTargets = level.maxTargets, maxSplashTargets = level.maxSplashTargets
                    };
                }

                string path = $"{_outputPath}Towers/Tower_{towerConfig.towerType}.asset";
                AssetDatabase.CreateAsset(towerData, path);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success", "Tower data generated!", "OK");
        }

        private void GenerateEnemyData()
        {
            if (_gameConfig == null) return;

            EnsureDirectoryExists();

            foreach (var enemyConfig in _gameConfig.enemies)
            {
                EnemyData enemyData = CreateInstance<EnemyData>();
                enemyData.enemyName = enemyConfig.enemyName;
                enemyData.enemyType = enemyConfig.enemyType;
                enemyData.stats = new EnemyData.EnemyStats
                {
                    // 在第393行添加转换
                    maxHealth = Mathf.RoundToInt(enemyConfig.stats.maxHealth),
                    moveSpeed = enemyConfig.stats.moveSpeed,
                    goldReward = enemyConfig.stats.goldReward,
                    lifeDamage = enemyConfig.stats.lifeDamage,
                    physicalResistance = enemyConfig.stats.physicalResistance,
                    magicResistance = enemyConfig.stats.magicResistance,
                    explosionResistance = enemyConfig.stats.explosionResistance,
                    isFlying = enemyConfig.stats.isFlying,
                    isArmored = enemyConfig.stats.isArmored,
                    isMagicImmune = enemyConfig.stats.isMagicImmune,
                    canBeSlowed = enemyConfig.stats.canBeSlowed,
                    slowImmunity = enemyConfig.stats.slowImmunity,
                    isBoss = enemyConfig.stats.isBoss,
                    bossHealthMultiplier = enemyConfig.stats.bossHealthMultiplier
                };

                string path = $"{_outputPath}Enemies/Enemy_{enemyConfig.enemyType}.asset";
                AssetDatabase.CreateAsset(enemyData, path);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success", "Enemy data generated!", "OK");
        }

        private void GenerateWaveData()
        {
            if (_gameConfig == null) return;

            EnsureDirectoryExists();

            foreach (var waveConfig in _gameConfig.waves)
            {
                WaveData waveData = CreateInstance<WaveData>();
                waveData.waveNumber = waveConfig.waveNumber;
                waveData.waveDelay = waveConfig.waveDelay;
                waveData.spawnInterval = waveConfig.spawnInterval;

                foreach (var waveEnemy in waveConfig.enemies)
                {
                    EnemyData enemyData = FindEnemyData(waveEnemy.enemyName);
                    if (enemyData != null)
                    {
                        waveData.enemies.Add(new WaveData.WaveEnemy
                        {
                            enemyData = enemyData,
                            count = waveEnemy.count,
                            spawnDelay = waveEnemy.spawnDelay,
                            spawnPoint = waveEnemy.spawnPoint
                        });
                    }
                }

                string path = $"{_outputPath}Waves/Wave_{waveConfig.waveNumber.ToString("D2")}.asset";
                AssetDatabase.CreateAsset(waveData, path);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success", "Wave data generated!", "OK");
        }

        private void GenerateLevelData()
        {
            if (_gameConfig == null) return;

            EnsureDirectoryExists();

            foreach (var levelConfig in _gameConfig.levels)
            {
                LevelData levelData = CreateInstance<LevelData>();
                levelData.levelName = levelConfig.levelName;
                levelData.sceneName = levelConfig.sceneName;
                levelData.initialGold = levelConfig.initialGold;
                levelData.initialLife = levelConfig.initialLife;
                levelData.timeBetweenWaves = levelConfig.timeBetweenWaves;
                levelData.isUnlocked = true;

                foreach (int waveIndex in levelConfig.waveIndices)
                {
                    if (waveIndex < _gameConfig.waves.Count)
                    {
                        WaveData waveData = FindWaveData(waveIndex + 1);
                        if (waveData != null)
                        {
                            levelData.waves.Add(waveData);
                        }
                    }
                }

                foreach (Vector3 slotPos in levelConfig.towerSlots)
                {
                    levelData.towerSlots.Add(new LevelData.TowerSlotData
                    {
                        position = slotPos,
                        slotType = LevelData.SlotType.Normal
                    });
                }

                foreach (var pathConfig in levelConfig.paths)
                {
                    levelData.paths.Add(new LevelData.PathData
                    {
                        pathName = pathConfig.pathName,
                        waypoints = pathConfig.waypoints
                    });
                }

                string path = $"{_outputPath}Levels/Level_{levelConfig.sceneName}.asset";
                AssetDatabase.CreateAsset(levelData, path);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success", "Level data generated!", "OK");
        }

        private void GenerateAll()
        {
            GenerateTowerData();
            GenerateEnemyData();
            GenerateWaveData();
            GenerateLevelData();
            EditorUtility.DisplayDialog("Success", "All data generated!", "OK");
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_outputPath))
            {
                Directory.CreateDirectory(_outputPath);
            }
            if (!Directory.Exists($"{_outputPath}Towers"))
            {
                Directory.CreateDirectory($"{_outputPath}Towers");
            }
            if (!Directory.Exists($"{_outputPath}Enemies"))
            {
                Directory.CreateDirectory($"{_outputPath}Enemies");
            }
            if (!Directory.Exists($"{_outputPath}Waves"))
            {
                Directory.CreateDirectory($"{_outputPath}Waves");
            }
            if (!Directory.Exists($"{_outputPath}Levels"))
            {
                Directory.CreateDirectory($"{_outputPath}Levels");
            }
        }

        private EnemyData FindEnemyData(string enemyName)
        {
            string[] guids = AssetDatabase.FindAssets($"t:EnemyData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                if (data != null && data.enemyName == enemyName)
                {
                    return data;
                }
            }
            return null;
        }

        private WaveData FindWaveData(int waveNumber)
        {
            string[] guids = AssetDatabase.FindAssets($"t:WaveData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                WaveData data = AssetDatabase.LoadAssetAtPath<WaveData>(path);
                if (data != null && data.waveNumber == waveNumber)
                {
                    return data;
                }
            }
            return null;
        }
    }
}
#endif

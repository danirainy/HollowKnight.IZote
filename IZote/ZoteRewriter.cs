﻿namespace IZote;
public class ZoteRewriter
{
    public void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
    {
        greyPrinceTemplate = preloadedObjects["GG_Grey_Prince_Zote"]["Grey Prince"];
        var battleControl = preloadedObjects["GG_Mighty_Zote"]["Battle Control"];
        var names = new List<(string, string, string)>{
            ("Zotelings", "Ordeal Zoteling", "Ordeal Zoteling"),
        };
        foreach ((string group, string instance, string name) in names)
        {
            GameObject minion;
            if (instance != "")
            {
                minion = battleControl.transform.Find(group).gameObject.transform.Find(instance).gameObject;
            }
            else
            {
                minion = battleControl.transform.Find(group).gameObject;
            }
            UnityEngine.Object.Destroy(minion.GetComponent<PersistentBoolItem>());
            UnityEngine.Object.Destroy(minion.GetComponent<ConstrainPosition>());
            prefabs[name] = minion;
            IZote.instance.Log("Minion added: " + minion.name + ".");
        }
    }
    private void RewriteInitStates(PlayMakerFSM control)
    {
        control.RemoveTransition("Init", "FINISHED");
        control.AddTransition("Init", "FINISHED", "Level 3");
        control.RemoveAction("Dormant", 3);
        control.AddCustomAction("Dormant", () => control.SetState("Enter 1"));
        control.RemoveAction("Enter 1", 5);
        control.RemoveAction("Enter 1", 3);
        control.AddCustomAction("Enter 1", () => control.SetState("Activate"));
        control.RemoveAction("Activate", 3);
        control.RemoveAction("Activate", 2);
        control.RemoveAction("Activate", 1);
        control.AddCustomAction("Activate", () =>
        {
            control.transform.localPosition = new Vector3(0.1f, 1.1f, 0.001f);
        });
        control.RemoveTransition("Activate", "FINISHED");
        control.AddTransition("Activate", "FINISHED", "Roar");
        var wait = new Wait
        {
            time = 3,
            realTime = false
        };
        foreach (var fsmEvent in control.FsmEvents)
        {
            if (fsmEvent.Name == "FINISHED")
            {
                wait.finishEvent = fsmEvent;
            }
        }
        control.RemoveAction("Roar", 7);
        control.RemoveAction("Roar", 4);
        control.RemoveAction("Roar", 0);
        control.AddAction("Roar", wait);
        control.AddTransition("Roar", "FINISHED", "Set Damage");
        control.AddCustomAction("Set Damage", () =>
        {
            control.GetAction("Roar End", 0).OnEnter();
            control.GetAction("Roar End", 2).OnEnter();
            ready = true;
        });
        control.RemoveTransition("Set Damage", "FINISHED");
    }
    private void RewriteStandStates(PlayMakerFSM control)
    {
        control.RemoveAction("Stand", 2);
        control.RemoveAction("Stand", 1);
        control.RemoveTransition("Stand", "FINISHED");
        control.RemoveTransition("Stand", "TOOK DAMAGE");
    }
    private void RewriteRunStates(PlayMakerFSM control)
    {
        var audioSpawnPoint = control.transform.Find("Audio Spawn Point").gameObject;
        control.GetAction<HutongGames.PlayMaker.Actions.AudioPlayerOneShot>("Run Antic", 1).spawnPoint = audioSpawnPoint;
        control.RemoveAction("Run", 7);
        control.RemoveAction("Run", 6);
        control.RemoveAction("Run", 5);
        control.RemoveAction("Run", 4);
        control.RemoveAction("Run", 3);
        control.RemoveAction("Run", 2);
        control.RemoveAction("Run", 0);
        control.RemoveTransition("Run", "FINISHED");
        control.RemoveTransition("Run", "TOOK DAMAGE");
    }
    private void RewriteJumpStates(PlayMakerFSM control)
    {
        var audioSpawnPoint = control.transform.Find("Audio Spawn Point").gameObject;
        control.GetAction<HutongGames.PlayMaker.Actions.AudioPlayerOneShot>("Jump", 1).spawnPoint = audioSpawnPoint;
        control.RemoveAction("Jump", 6);
        control.RemoveAction("Jump", 5);
        control.RemoveAction("Jump", 4);
        control.RemoveAction("Jump", 3);
        control.InsertCustomAction("Jump", () =>
        {
            if (HeroController.instance.cState.jumping)
            {
                control.GetAction<HutongGames.PlayMaker.Actions.AudioPlayerOneShot>("Jump", 2).volume = 1;
            }
            else
            {
                control.GetAction<HutongGames.PlayMaker.Actions.AudioPlayerOneShot>("Jump", 2).volume = 0;
            }
        }, 0);
        control.RemoveTransition("Jump", "FINISHED");
    }
    private void UpdateShockWave(PlayMakerFSM control)
    {
        var shockWave = control.FsmVariables.FindFsmGameObject("Shockwave").Value;
        while (waveQueue.Count > 3)
        {
            UnityEngine.Object.Destroy(waveQueue[0]);
            waveQueue.RemoveAt(0);
        }
        waveQueue.Add(shockWave);
        IZote.instance.Log("Current number of waves is " + waveQueue.Count + ".");
        shockWave.transform.SetPositionY(control.transform.position.y - 4);
        var spawn = shockWave.LocateMyFSM("Spawn");
        spawn.AddCustomAction("Spawn", () =>
        {
            var spurt = spawn.FsmVariables.FindFsmGameObject("Spurt").Value;
            spurt.RemoveComponent<DamageHero>();
            var damageEnemiesCharge = spurt.AddComponent<DamageEnemies>();
            var damageEnemiesSlash = HeroController.instance.gameObject.Find("Attacks").Find("Slash").LocateMyFSM("damages_enemy");
            damageEnemiesCharge.attackType = AttackTypes.Spell;
            damageEnemiesCharge.circleDirection = damageEnemiesSlash.FsmVariables.GetFsmBool("circleDirection").Value;
            damageEnemiesCharge.damageDealt = damageEnemiesSlash.FsmVariables.GetFsmInt("damageDealt").Value;
            damageEnemiesCharge.direction = damageEnemiesSlash.FsmVariables.GetFsmFloat("direction").Value;
            damageEnemiesCharge.ignoreInvuln = damageEnemiesSlash.FsmVariables.GetFsmBool("Ignore Invuln").Value;
            damageEnemiesCharge.magnitudeMult = damageEnemiesSlash.FsmVariables.GetFsmFloat("magnitudeMult").Value;
            damageEnemiesCharge.moveDirection = damageEnemiesSlash.FsmVariables.GetFsmBool("moveDirection").Value;
            damageEnemiesCharge.specialType = (SpecialTypes)damageEnemiesSlash.FsmVariables.GetFsmInt("Special Type").Value;
        });
    }
    private void RewriteLandStates(PlayMakerFSM control)
    {
        var spawnHack = new SpawnHack();
        spawnHack.CopyFrom(control.GetState("Land Waves").Actions[0] as SpawnObjectFromGlobalPool);
        control.GetState("Land Waves").Actions[0] = spawnHack;
        spawnHack = new SpawnHack();
        spawnHack.CopyFrom(control.GetState("Land Waves").Actions[6] as SpawnObjectFromGlobalPool);
        control.GetState("Land Waves").Actions[6] = spawnHack;
        control.InsertCustomAction("Land Waves", () =>
        {
            UpdateShockWave(control);
        }, 9);
        control.InsertCustomAction("Land Waves", () =>
        {
            UpdateShockWave(control);
        }, 3);
        var audioSpawnPoint = control.transform.Find("Audio Spawn Point").gameObject;
        control.GetAction<HutongGames.PlayMaker.Actions.AudioPlayerOneShotSingle>("Land Normal", 3).spawnPoint = audioSpawnPoint;
        control.RemoveAction("Land Normal", 4);
        control.RemoveAction("Land Normal", 1);
        control.AddCustomAction("Land Normal", () =>
        {
            var slamEffect = control.gameObject.Find("Slam Effect");
            if (slamEffectNew != null)
            {
                UnityEngine.Object.Destroy(slamEffectNew);
            }
            slamEffectNew = UnityEngine.Object.Instantiate(slamEffect);
            slamEffectNew.transform.localPosition = slamEffect.transform.position;
            slamEffectNew.transform.localScale = slamEffect.transform.lossyScale;
            slamEffectNew.SetActive(true);
            slamEffectNew.name = "Slam Effect New";
        });
        control.RemoveTransition("Land Normal", "FINISHED");
        control.AddTransition("Land Normal", "FINISHED", "Stand");
        var slamEffect = control.gameObject.Find("Slam Effect");
        slamEffect.transform.localPosition = new Vector3(-0.39f, -2.8f, 0.01f);
    }
    private void RewriteStompStates(PlayMakerFSM control)
    {
        control.RemoveAction("Stomp", 4);
        control.RemoveAction("Stomp", 3);
        control.RemoveAction("Stomp", 2);
        control.RemoveAction("Stomp", 1);
    }
    private void RewriteSlashStates(PlayMakerFSM control)
    {
        var stompHit = control.gameObject.Find("Stomp Hit");
        stompHit.layer = LayerMask.NameToLayer("Attack");
        stompHit.RemoveComponent<DamageHero>();
        stompHit.AddComponent<DamageEnemies>();
        control.GetAction<SetScale>("Slash Waves L", 3).x = 4;
        control.GetAction<SetScale>("Slash Waves R", 3).x = 4;
        var spawnHack = new SpawnHack();
        spawnHack.CopyFrom(control.GetState("Slash Waves L").Actions[0] as SpawnObjectFromGlobalPool);
        control.GetState("Slash Waves L").Actions[0] = spawnHack;
        spawnHack = new SpawnHack();
        spawnHack.CopyFrom(control.GetState("Slash Waves L").Actions[6] as SpawnObjectFromGlobalPool);
        control.GetState("Slash Waves L").Actions[6] = spawnHack;
        spawnHack = new SpawnHack();
        spawnHack.CopyFrom(control.GetState("Slash Waves R").Actions[0] as SpawnObjectFromGlobalPool);
        control.GetState("Slash Waves R").Actions[0] = spawnHack;
        spawnHack = new SpawnHack();
        spawnHack.CopyFrom(control.GetState("Slash Waves R").Actions[6] as SpawnObjectFromGlobalPool);
        control.GetState("Slash Waves R").Actions[6] = spawnHack;
        control.InsertCustomAction("Slash Waves L", () =>
        {
            UpdateShockWave(control);
        }, 9);
        control.InsertCustomAction("Slash Waves L", () =>
        {
            UpdateShockWave(control);
        }, 3);
        control.InsertCustomAction("Slash Waves R", () =>
        {
            UpdateShockWave(control);
        }, 9);
        control.InsertCustomAction("Slash Waves R", () =>
        {
            UpdateShockWave(control);
        }, 3);
        control.RemoveAction("Stomp Slash", 4);
        control.RemoveAction("Stomp Slash", 3);
        control.RemoveAction("Stomp Slash", 2);
        control.InsertCustomAction("Stomp Slash", () =>
        {
            var damageEnemiesCharge = stompHit.GetComponent<DamageEnemies>();
            var damageEnemiesSlash = HeroController.instance.gameObject.Find("Attacks").Find("Slash").LocateMyFSM("damages_enemy");
            damageEnemiesCharge.attackType = (AttackTypes)damageEnemiesSlash.FsmVariables.GetFsmInt("attackType").Value;
            damageEnemiesCharge.circleDirection = damageEnemiesSlash.FsmVariables.GetFsmBool("circleDirection").Value;
            damageEnemiesCharge.damageDealt = (int)(damageEnemiesSlash.FsmVariables.GetFsmInt("damageDealt").Value * 2.5f);
            damageEnemiesCharge.direction = damageEnemiesSlash.FsmVariables.GetFsmFloat("direction").Value;
            damageEnemiesCharge.ignoreInvuln = damageEnemiesSlash.FsmVariables.GetFsmBool("Ignore Invuln").Value;
            damageEnemiesCharge.magnitudeMult = damageEnemiesSlash.FsmVariables.GetFsmFloat("magnitudeMult").Value;
            damageEnemiesCharge.moveDirection = damageEnemiesSlash.FsmVariables.GetFsmBool("moveDirection").Value;
            damageEnemiesCharge.specialType = (SpecialTypes)damageEnemiesSlash.FsmVariables.GetFsmInt("Special Type").Value;
        }, 0);
        control.RemoveTransition("Slash End", "FINISHED");
        control.AddTransition("Slash End", "FINISHED", "Stand");
    }
    private void RewriteChargeStates(PlayMakerFSM control)
    {
        var chargeHit = control.gameObject.Find("Charge Hit");
        chargeHit.layer = LayerMask.NameToLayer("Attack");
        chargeHit.RemoveComponent<DamageHero>();
        chargeHit.AddComponent<DamageEnemies>();
        var audioSpawnPoint = control.transform.Find("Audio Spawn Point").gameObject;
        control.GetAction<HutongGames.PlayMaker.Actions.AudioPlayerOneShotSingle>("Charge Antic", 3).spawnPoint = audioSpawnPoint;
        control.RemoveAction("Charge Antic", 2);
        control.RemoveAction("Charge Antic", 1);
        control.AddCustomAction("Charge Antic", () =>
        {
            var damageEnemiesCharge = chargeHit.GetComponent<DamageEnemies>();
            var damageEnemiesSlash = HeroController.instance.gameObject.Find("Attacks").Find("Slash").LocateMyFSM("damages_enemy");
            damageEnemiesCharge.attackType = (AttackTypes)damageEnemiesSlash.FsmVariables.GetFsmInt("attackType").Value;
            damageEnemiesCharge.circleDirection = damageEnemiesSlash.FsmVariables.GetFsmBool("circleDirection").Value;
            damageEnemiesCharge.damageDealt = damageEnemiesSlash.FsmVariables.GetFsmInt("damageDealt").Value;
            damageEnemiesCharge.direction = damageEnemiesSlash.FsmVariables.GetFsmFloat("direction").Value;
            damageEnemiesCharge.ignoreInvuln = damageEnemiesSlash.FsmVariables.GetFsmBool("Ignore Invuln").Value;
            damageEnemiesCharge.magnitudeMult = damageEnemiesSlash.FsmVariables.GetFsmFloat("magnitudeMult").Value;
            damageEnemiesCharge.moveDirection = damageEnemiesSlash.FsmVariables.GetFsmBool("moveDirection").Value;
            damageEnemiesCharge.specialType = (SpecialTypes)damageEnemiesSlash.FsmVariables.GetFsmInt("Special Type").Value;
        });
        control.RemoveAction("Charge Start", 7);
        control.RemoveAction("Charge Start", 6);
        control.RemoveAction("Charge Start", 5);
        control.RemoveAction("Charge Start", 0);
        control.RemoveTransition("Charge Start", "FINISHED");
        control.RemoveTransition("Charge Start", "L");
        control.RemoveTransition("Charge Start", "R");
        control.RemoveTransition("Charge Start", "FAIL");
    }
    private void RewriteDashStates(PlayMakerFSM control)
    {
        control.GetAction<HutongGames.PlayMaker.Actions.Tk2dPlayAnimationWithEvents>("Stomp Shift L", 3).animationCompleteEvent = null;
        control.RemoveAction("Stomp Shift L", 2);
        control.RemoveAction("Stomp Shift L", 1);
        control.RemoveAction("Stomp Shift L", 0);
    }
    private void RewriteSpitStates(PlayMakerFSM control)
    {
        control.RemoveAction("Spit Antic", 6);
        control.RemoveAction("Spit Antic", 5);
        control.RemoveAction("Spit Antic", 3);
        control.RemoveAction("Spit Antic", 2);
        control.RemoveAction("Spit Antic", 1);
        control.RemoveAction("Spit Antic", 0);
        control.RemoveTransition("Spit Antic", "FINISHED");
        control.AddTransition("Spit Antic", "FINISHED", "Spit L");
        control.RemoveAction("Spit L", 7);
        control.RemoveAction("Spit L", 1);
        control.RemoveAction("Spit L", 0);
        control.InsertCustomAction("Spit L", () =>
        {
            var zoteling = UnityEngine.Object.Instantiate(prefabs["Ordeal Zoteling"]);
            var fsm = zoteling.LocateMyFSM("Control");
            IZote.instance.Log("Upgrading FSM: " + fsm.gameObject.name + " - " + fsm.FsmName + ".");
            fsm.AddTransition("Dormant", "FINISHED", "Ball");
            fsm.RemoveAction("Ball", 2);
            fsm.AddCustomAction("Reset", () =>
            {
                UnityEngine.Object.Destroy(fsm.gameObject);
            });
            IZote.instance.Log("Upgraded FSM: " + fsm.gameObject.name + " - " + fsm.FsmName + ".");
            control.FsmVariables.FindFsmGameObject("Zoteling").Value = zoteling;
            UnityEngine.Object.Destroy(zoteling.GetComponent<DamageHero>());
            UnityEngine.Object.Destroy(zoteling.GetComponent<EnemyDeathEffectsUninfected>());
            UnityEngine.Object.Destroy(zoteling.GetComponent<EnemyHitEffectsUninfected>());
            UnityEngine.Object.Destroy(zoteling.GetComponent<HealthManager>());
            UnityEngine.Object.Destroy(zoteling.GetComponent<EnemyDreamnailReaction>());
            zoteling.layer = LayerMask.NameToLayer("Attack");
            zoteling.GetComponent<BoxCollider2D>().isTrigger = true;
            var damageEnemiesCharge = zoteling.AddComponent<DamageEnemies>();
            var damageEnemiesSlash = HeroController.instance.gameObject.Find("Attacks").Find("Slash").LocateMyFSM("damages_enemy");
            damageEnemiesCharge.attackType = AttackTypes.Spell;
            damageEnemiesCharge.circleDirection = damageEnemiesSlash.FsmVariables.GetFsmBool("circleDirection").Value;
            damageEnemiesCharge.damageDealt = damageEnemiesSlash.FsmVariables.GetFsmInt("damageDealt").Value;
            damageEnemiesCharge.direction = damageEnemiesSlash.FsmVariables.GetFsmFloat("direction").Value;
            damageEnemiesCharge.ignoreInvuln = damageEnemiesSlash.FsmVariables.GetFsmBool("Ignore Invuln").Value;
            damageEnemiesCharge.magnitudeMult = damageEnemiesSlash.FsmVariables.GetFsmFloat("magnitudeMult").Value;
            damageEnemiesCharge.moveDirection = damageEnemiesSlash.FsmVariables.GetFsmBool("moveDirection").Value;
            damageEnemiesCharge.specialType = (SpecialTypes)damageEnemiesSlash.FsmVariables.GetFsmInt("Special Type").Value;
            summonQueue.Add(zoteling);
            if (summonQueue.Count > 2)
            {
                UnityEngine.Object.Destroy(summonQueue[0]);
                summonQueue.RemoveAt(0);
            }
            var getTrackingEnemy = () =>
            {
                if (currentEnemy == null)
                {
                    foreach (var root in zoteling.scene.GetAllGameObjects())
                    {
                        foreach (var hm in root.GetComponentsInChildren<HealthManager>())
                        {
                            if (hm.hp > 0)
                            {
                                currentEnemy = hm.gameObject;
                                return currentEnemy;
                            }
                        }
                    }
                    return HeroController.instance.gameObject;
                }
                else
                {
                    return currentEnemy;
                }
            };
            fsm.InsertCustomAction("Buzzer Chase", () =>
            {
                fsm.GetAction<ChaseObject>("Buzzer Chase", 2).target.Value = getTrackingEnemy();
            }, 0);
            fsm.InsertCustomAction("Dir", () =>
            {
                fsm.GetAction<CheckTargetDirection>("Dir", 1).target.Value = getTrackingEnemy();
            }, 0);
            fsm.GetAction<SendRandomEvent>("Choice", 3).weights[1] = 0;
        }, 0);
        control.RemoveTransition("Spit Recover", "FINISHED");
        control.AddTransition("Spit Recover", "FINISHED", "Stand");
    }
    public void Enter()
    {
        var knight = HeroController.instance.gameObject;
        var greyPrince = UnityEngine.Object.Instantiate(greyPrinceTemplate, knight.transform);
        greyPrince.SetActive(true);
        greyPrince.name = "Grey Prince";
        greyPrince.transform.localScale = new Vector3(-1, 1, 1);
        UnityEngine.Object.Destroy(greyPrince.LocateMyFSM("Constrain X"));
        UnityEngine.Object.Destroy(greyPrince.GetComponent<DamageHero>());
        UnityEngine.Object.Destroy(greyPrince.GetComponent<Rigidbody2D>());
        UnityEngine.Object.Destroy(greyPrince.GetComponent<EnemyDeathEffectsUninfected>());
        UnityEngine.Object.Destroy(greyPrince.GetComponent<EnemyHitEffectsUninfected>());
        UnityEngine.Object.Destroy(greyPrince.GetComponent<HealthManager>());
        UnityEngine.Object.Destroy(greyPrince.GetComponent<EnemyDreamnailReaction>());
        var audioSpawnPoint = new GameObject();
        audioSpawnPoint.name = "Audio Spawn Point";
        audioSpawnPoint.transform.parent = greyPrince.transform;
        audioSpawnPoint.transform.localPosition = new Vector3(0, 1, 0);
        var control = greyPrince.LocateMyFSM("Control");
        RewriteInitStates(control);
        RewriteStandStates(control);
        RewriteRunStates(control);
        RewriteJumpStates(control);
        RewriteLandStates(control);
        RewriteStompStates(control);
        RewriteSlashStates(control);
        RewriteChargeStates(control);
        RewriteDashStates(control);
        RewriteSpitStates(control);
        foreach (var state in control.FsmStates)
        {
            state.AddCustomAction(() =>
            {
                IZote.instance.Log("Leaving state " + state.Name + ".");
            });
        }
        ready = false;
    }
    public void Exit()
    {
        var knight = HeroController.instance.gameObject;
        var greyPrinceTransform = knight.transform.Find("Grey Prince");
        var greyPrince = greyPrinceTransform.gameObject;
        UnityEngine.Object.Destroy(greyPrince);
        ready = false;
    }
    private GameObject greyPrinceTemplate;
    public bool ready;
    public GameObject slamEffectNew;
    private List<GameObject> waveQueue = new();
    private List<GameObject> summonQueue = new();
    private readonly Dictionary<string, GameObject> prefabs = new();
    private GameObject currentEnemy;
}

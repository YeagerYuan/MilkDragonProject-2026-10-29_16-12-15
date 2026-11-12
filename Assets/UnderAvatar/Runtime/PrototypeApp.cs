using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderAvatar
{
    public enum TestRole { Reporter, Streamer }
    public sealed class PrototypeApp : MonoBehaviour
    {
        [SerializeField] BuildingAsset buildingAsset;
        [SerializeField] TestRole testRole;
        [SerializeField] GameAudioProfile audioProfile;
        [SerializeField] CharacterVisualProfile characterVisuals;
        [SerializeField] BuildingAsset trainingHall;
        public BuildingAsset TrainingBuilding => trainingHall?trainingHall:Resources.Load<BuildingAsset>("TrainingHall");
        public CharacterVisualProfile CharacterVisuals => characterVisuals?characterVisuals:Resources.Load<CharacterVisualProfile>("CharacterVisuals");
        public GameAudioProfile AudioProfile => audioProfile?audioProfile:Resources.Load<GameAudioProfile>("GameAudio");
        [SerializeField] MatchSettings settings=new MatchSettings();
        public BuildingAsset Building { get => buildingAsset; set => buildingAsset=value; }
        public TestRole Role { get => testRole; set => testRole=value; }
        public BuildingWorld World { get; private set; }
        public MatchSettings Settings => settings;
        public MatchSettings RuntimeSettings { get; private set; }
        public LanGame Lan { get; private set; }
        public MatchSession Session { get; private set; }
        public string Status { get; private set; }
        public string StartupError { get; private set; }
        public bool SingleReady { get; private set; }
        public const string VolumePreference="UnderAvatar.MasterVolume";
        public bool SettingsOpen { get; private set; }
        public bool GameplayInputBlocked => ComicOpen || SettingsOpen || Time.frameCount==settingsClosedFrame;
        public float MasterVolume => AudioListener.volume;
        public MenuAudio MenuSound { get; private set; }
        public DisplayModeSettings DisplayMode { get; private set; }
        [Header("狗仔开场漫画")]
        [Tooltip("留空使用 Resources/PaparazziComicCanvas；可在 Comic Builder 中重新生成。")]
        [SerializeField] ComicPlayerUI paparazziComicPrefab;
        public ComicPlayerUI Comic { get; private set; }
        public bool ComicOpen => Comic && Comic.IsPlaying;
        public void BeforeTrainingHall(TestRole role,Action enter)
        {
            if(ComicOpen) return;
            if(role!=TestRole.Reporter) { enter();return; }
            CloseSettings();
            if(!Comic)
            {
                var prefab=paparazziComicPrefab?paparazziComicPrefab:Resources.Load<ComicPlayerUI>("PaparazziComicCanvas");
                if(!prefab) throw new InvalidOperationException("缺少漫画预制体，请运行 Tools > Under Avatar > Comic Builder。");
                Comic=Instantiate(prefab,transform,false);
            }
            Comic.Show(()=> {settingsClosedFrame=Time.frameCount;enter();},()=>MenuSound.PlayPageTurn());
        }
        public void CancelComic() { if(Comic) Comic.Cancel(); }
        float previousTimeScale=1;
        int settingsClosedFrame=-1;
        public void SetMasterVolume(float value)
        {
            AudioListener.volume=Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumePreference,AudioListener.volume);
        }
        public void OpenSettings()
        {
            if(SettingsOpen || ComicOpen) return;
            SettingsOpen=true;previousTimeScale=Time.timeScale;
            if(Session && !Session.IsNetwork) Time.timeScale=0;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public void CloseSettings()
        {
            if(!SettingsOpen) return;
            SettingsOpen=false;settingsClosedFrame=Time.frameCount;
            Time.timeScale=previousTimeScale;PlayerPrefs.Save();
            bool release=!Session || Session.Finished || Session.DebugOpen || Session.LocalPhoneMenuOpen || (Session.HouseMap && Session.HouseMap.IsOpen) || DocumentManager.BlocksGameplayInput;
            Cursor.lockState=release?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=release;
        }
        public bool CanStartSingleMatch => Session && !Session.IsNetwork && Session.IsTraining && SingleReady;
        GameObject matchRoot;
        Camera menuCamera;
        AudioListener menuListener;
        DarkWorldLighting worldLighting;
        [SerializeField] bool useDvAtmosphere = true;
        [Header("文档阅读")]
        [SerializeField] bool enableDocuments=true;
        [Tooltip("留空使用 Resources/DocumentReader；文档阅读界面预制体。")]
        [SerializeField] GameObject documentReaderPrefab;
        [Tooltip("正式关卡中的文档布置；留空使用 Resources/DeveloperDocuments。可打开预制体编辑位置。")]
        [SerializeField] GameObject documentPropsPrefab;
        public bool DocumentsEnabled => enableDocuments;
        public GameObject DocumentPropsPrefab => documentPropsPrefab?documentPropsPrefab:Resources.Load<GameObject>("DeveloperDocuments");

        void Awake()
        {
            DisplayMode=gameObject.AddComponent<DisplayModeSettings>();
            AudioListener.volume=Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreference,1));
            Application.targetFrameRate=90;
            Application.runInBackground=true;
            ClearEditorBuildingPreviews();
            Lan=gameObject.AddComponent<LanGame>();Lan.Initialize(this);
            var menu=new GameObject("MenuCamera");menu.transform.SetParent(transform,false);
            menuCamera=menu.AddComponent<Camera>();menuCamera.clearFlags=CameraClearFlags.SolidColor;
            menuListener=menu.AddComponent<AudioListener>();
            MenuSound=gameObject.AddComponent<MenuAudio>();MenuSound.Initialize(this);
            menuCamera.backgroundColor=new Color(.025f,.045f,.065f);menuCamera.cullingMask=0;menuCamera.depth=-100;
            var root=new GameObject("BuildingWorld");
            root.transform.SetParent(transform,false);
            World=root.AddComponent<BuildingWorld>();
            worldLighting=gameObject.AddComponent<DarkWorldLighting>();
            gameObject.AddComponent<PrototypeHud>().Initialize(this);
            if(enableDocuments && !DocumentManager.Instance)
            {
                var reader=documentReaderPrefab?documentReaderPrefab:Resources.Load<GameObject>("DocumentReader");
                if(reader) Instantiate(reader,transform,false);
            }
        }

        void ClearEditorBuildingPreviews()
        {
            // A generated house-editor preview can survive an interrupted Play Mode transition.
            // It is separate from the runtime BuildingWorld and would otherwise render beside the hall.
            // foreach(var marker in FindObjectsByType<BuildingPreviewMarker>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            // {
            //     if(!marker || marker.gameObject.scene!=gameObject.scene) continue;
            //     marker.gameObject.SetActive(false);
            //     Destroy(marker.gameObject);
            // }
        }

        void Start() { if(!Session) { Cursor.lockState=CursorLockMode.None;Cursor.visible=true; } }
        void Update()
        {
            if(!ComicOpen && !Session && Keyboard.current?.escapeKey.wasPressedThisFrame==true)
            { if(SettingsOpen) CloseSettings();else OpenSettings(); }
        }
        public void Report(string message) { Status=message; }
        public static void ValidateMap(BuildingData data)
        {
            data.Validate();StairLayout.Validate(data);
            if(data.useTaskEditor && !data.tasks.Any(t=>!string.IsNullOrWhiteSpace(t.targetId)) && !data.requireLiveEvidence)
                throw new InvalidOperationException("至少配置一项已绑定目标的任务，或启用直播证据要求。");
            if(data.floors.SelectMany(f=>f.items).Count(i=>i.type==ItemType.StreamerPC)!=1)
                throw new InvalidOperationException("游玩地图必须有且仅有一个主播电脑桌。");
        }
        public void StartSingle(TestRole role)
        {
            if(ComicOpen) return;
            Lan.Disconnect();StopMatchForLobby();Role=role;RuntimeSettings=null;
            BeforeTrainingHall(role,()=>CreateMatch(TrainingBuilding?TrainingBuilding.CreateRuntimeCopy():TrainingHallLayout.Create(),false,true,true));
        }
        public void SetSingleReady(bool ready)
        {
            if(Session && !Session.IsNetwork && Session.IsTraining) SingleReady=ready;
        }
        public void StartSingleMatch()
        {
            if(!CanStartSingleMatch) return;
            try {ValidateMap(buildingAsset?buildingAsset.CreateRuntimeCopy():BuildingData.CreateDefault());}
            catch(Exception error)
            {
                StartupError="无法进入正式对局："+error.Message;Report(StartupError);
                Session.Notice(StartupError,12);Session.DebugOpen=true;Session.Reporter.SetCursor(true);return;
            }
            var hall=World.Data.Copy();
            DisposeMatch();EnterPlay();
            if(!Session)
            {
                var failure=StartupError;CreateMatch(hall,false,true,true);StartupError=failure;SingleReady=true;
                if(Session) {Session.DebugOpen=true;Session.Notice(failure,12);Session.Reporter.SetCursor(true);}
            }
        }
        public void StartNetworkSession(BuildingData data,MatchSettings rules,TestRole role,bool authority,bool training=false)
        {
            DisposeMatch();Role=role;
            RuntimeSettings=JsonUtility.FromJson<MatchSettings>(JsonUtility.ToJson(rules));
            CreateMatch(data.Copy(),true,authority,training);
        }

        public void EnterPlay()
        {
            if(Session) return;
            RuntimeSettings=null;
            CreateMatch(buildingAsset?buildingAsset.CreateRuntimeCopy():BuildingData.CreateDefault(),false,true);
        }
        void CreateMatch(BuildingData data,bool networked,bool authority,bool training=false)
        {
            StartupError=null;
            try
            {
                // Gameplay receives a copy. Doors, pickups and resets never mutate the authored asset.
                ValidateMap(data);
                var drafts=data.useTaskEditor?data.tasks.Where(t=>string.IsNullOrWhiteSpace(t.targetId)).Select(t=>t.title).ToArray():Array.Empty<string>();
                if(data.useTaskEditor) data.tasks.RemoveAll(t=>string.IsNullOrWhiteSpace(t.targetId));
                menuCamera.enabled=false;menuListener.enabled=false;World.gameObject.SetActive(true);
                World.Rebuild(data);
                World.SetMode(false,0);
                Vector3 spawn=data.useCustomReporterSpawn?data.ReporterSpawnPosition:World.FindFreePosition(0);
                matchRoot=new GameObject("MatchSession");
                matchRoot.transform.SetParent(transform,false);
                Session=matchRoot.AddComponent<MatchSession>();
                Session.Initialize(this,spawn,networked,authority,training);
                if(data.useCustomReporterSpawn)
                    Session.Reporter.SetLookAt(Session.Reporter.transform.position+Quaternion.Euler(0,data.reporterSpawnYaw,0)*Vector3.forward*4+Vector3.up*1.7f);
                if (useDvAtmosphere && !training)
                {
                    worldLighting.SetHorrorAtmosphere(true);
                    var camera = Role == TestRole.Reporter ? Session.Reporter.View : Session.Streamer.View;
                    var display = camera.gameObject.AddComponent<DVCamcorderDisplay>();
                    display.UsePreset(DvPreset(Role));
                    if(Role==TestRole.Reporter && !networked)
                    {
                        var observer=Session.Streamer.View.gameObject.AddComponent<DVCamcorderDisplay>();
                        observer.EnableObserverOutput();
                        observer.UsePreset(DvPreset(TestRole.Streamer));
                    }
                    Session.Reporter.Vision.gameObject.AddComponent<HorrorFlashlightController>();
                    Session.Streamer.Vision.gameObject.AddComponent<HorrorFlashlightController>();
                }
                GameObject documentRoot=null;
                if(enableDocuments && !training)
                {
                    var documents=DocumentPropsPrefab;
                    if(documents) documentRoot=Instantiate(documents,matchRoot.transform,false);
                }
                if(!training) Session.RegisterDocumentTargets(documentRoot);
                if(data.useCustomReporterSpawn)
                {
                    Physics.SyncTransforms();
                    foreach(var obstacle in Physics.OverlapCapsule(spawn+Vector3.up*.4f,spawn+Vector3.up*1.5f,.32f,PrototypeLayers.SolidMask,QueryTriggerInteraction.Ignore))
                        if(!obstacle.transform.IsChildOf(Session.Reporter.transform) && !obstacle.transform.IsChildOf(Session.Streamer.transform))
                            throw new ArgumentException("狗仔出生点与场景物体重叠，请在关卡编辑器中移到空地。");
                }
                Session.InitializeReporterLives();
                if(training) { Report("教学大厅：自由练习交互，Tab 打开准备菜单。");return; }
                if(drafts.Length>0) Session.Notice("已跳过未绑定目标的草稿任务："+string.Join("、",drafts),12);
                Report(Role==TestRole.Streamer
                    ? "主播测试：空格 / 左键拍翅；达到目标分数后，WASD 搜捕。Tab 调试，Esc 设置。"
                    : "WASD 行走 · Shift 静步 · Ctrl 蹲下 · E 交互 · F 手机 · 左键/空格拍照 · Tab 调试。");
            }
            catch(Exception e)
            {
                DisposeMatch();
                if(World) World.gameObject.SetActive(false);
                if(menuCamera) menuCamera.enabled=true;if(menuListener) menuListener.enabled=true;
                StartupError="无法开始测试："+e.Message;
                Report(StartupError);
                Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            }
        }

        static Material DvPreset(TestRole role) => Resources.Load<Material>(role==TestRole.Reporter
            ?"HorrorDVSignal_Reporter":"HorrorDVSignal_Streamer") ?? Resources.Load<Material>("HorrorDVSignal");

        void DisposeMatch()
        {
            CloseSettings();
            if(DocumentManager.Instance && DocumentManager.Instance.Owner && DocumentManager.Instance.Owner.App==this)
                DocumentManager.Instance.CloseForSessionEnd();
            if(matchRoot) { matchRoot.SetActive(false); PrototypeVisuals.Release(matchRoot); }
            matchRoot=null; Session=null;SingleReady=false;
            if (worldLighting) worldLighting.SetHorrorAtmosphere(false);
        }
        public void StopMatchForLobby()
        {
            CancelComic();DisposeMatch();RuntimeSettings=null;StartupError=null;
            World.gameObject.SetActive(false);menuCamera.enabled=true;menuListener.enabled=true;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public void ReturnToMenu() { Lan.Disconnect();StopMatchForLobby(); }

        public void RestartGame() { if(Session && Session.IsNetwork) Lan.ReturnToLobby();else StartSingle(Role); }
        public void ExitPlay() { ReturnToMenu(); }
        public void QuitTest()
        {
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        void OnDestroy() { CancelComic();CloseSettings();Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
    }
}

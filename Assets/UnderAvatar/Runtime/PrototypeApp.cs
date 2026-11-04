using System;
using System.Linq;
using UnityEngine;

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
        public bool CanStartSingleMatch => Session && !Session.IsNetwork && Session.IsTraining && SingleReady;
        GameObject matchRoot;
        Camera menuCamera;
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
            Application.targetFrameRate=90;
            Application.runInBackground=true;
            Lan=gameObject.AddComponent<LanGame>();Lan.Initialize(this);
            var menu=new GameObject("MenuCamera");menu.transform.SetParent(transform,false);
            menuCamera=menu.AddComponent<Camera>();menuCamera.clearFlags=CameraClearFlags.SolidColor;
            menuCamera.backgroundColor=new Color(.025f,.045f,.065f);menuCamera.cullingMask=0;menuCamera.depth=-100;
            var root=new GameObject("BuildingWorld");
            root.transform.SetParent(transform,false);
            World=root.AddComponent<BuildingWorld>();
            gameObject.AddComponent<DarkWorldLighting>();
            gameObject.AddComponent<PrototypeHud>().Initialize(this);
            if(enableDocuments && !DocumentManager.Instance)
            {
                var reader=documentReaderPrefab?documentReaderPrefab:Resources.Load<GameObject>("DocumentReader");
                if(reader) Instantiate(reader,transform,false);
            }
        }

        void Start() { if(!Session) { Cursor.lockState=CursorLockMode.None;Cursor.visible=true; } }
        public void Report(string message) { Status=message; }
        public static void ValidateMap(BuildingData data)
        {
            data.Validate();StairLayout.Validate(data);
            if(data.useTaskEditor && data.tasks.Any(t=>string.IsNullOrEmpty(t.targetId)))
                throw new InvalidOperationException("任务尚未绑定目标物体，请在任务编辑器中完成配置。");
            if(data.floors.SelectMany(f=>f.items).Count(i=>i.type==ItemType.StreamerPC)!=1)
                throw new InvalidOperationException("游玩地图必须有且仅有一个主播电脑桌。");
        }
        public void StartSingle(TestRole role)
        {
            Lan.Disconnect();DisposeMatch();Role=role;RuntimeSettings=null;
            CreateMatch(TrainingBuilding?TrainingBuilding.CreateRuntimeCopy():TrainingHallLayout.Create(),false,true,true);
        }
        public void SetSingleReady(bool ready)
        {
            if(Session && !Session.IsNetwork && Session.IsTraining) SingleReady=ready;
        }
        public void StartSingleMatch()
        {
            if(!CanStartSingleMatch) return;
            DisposeMatch();EnterPlay();
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
                menuCamera.enabled=false;World.gameObject.SetActive(true);
                World.Rebuild(data);
                World.SetMode(false,0);
                Vector3 spawn=World.FindFreePosition(0);
                matchRoot=new GameObject("MatchSession");
                matchRoot.transform.SetParent(transform,false);
                Session=matchRoot.AddComponent<MatchSession>();
                Session.Initialize(this,spawn,networked,authority,training);
                GameObject documentRoot=null;
                if(enableDocuments && !training)
                {
                    var documents=DocumentPropsPrefab;
                    if(documents) documentRoot=Instantiate(documents,matchRoot.transform,false);
                }
                if(!training) Session.RegisterDocumentTargets(documentRoot);
                if(training) { Report("教学大厅：自由练习交互，Tab 打开准备菜单。");return; }
                Report(Role==TestRole.Streamer
                    ? "主播测试：空格 / 左键拍翅；达到目标分数后，WASD 搜捕。Tab 调试，Esc 退出。"
                    : "WASD 行走 · Shift 静步 · Ctrl 蹲下 · E 交互 · F 手机 · 左键/空格拍照 · Tab 调试。");
            }
            catch(Exception e)
            {
                DisposeMatch();
                if(World) World.gameObject.SetActive(false);
                if(menuCamera) menuCamera.enabled=true;
                StartupError="无法开始测试："+e.Message;
                Report(StartupError);
                Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            }
        }

        void DisposeMatch()
        {
            if(DocumentManager.Instance && DocumentManager.Instance.Owner && DocumentManager.Instance.Owner.App==this)
                DocumentManager.Instance.CloseForSessionEnd();
            if(matchRoot) { matchRoot.SetActive(false); PrototypeVisuals.Release(matchRoot); }
            matchRoot=null; Session=null;SingleReady=false;
        }
        public void StopMatchForLobby()
        {
            DisposeMatch();RuntimeSettings=null;StartupError=null;
            World.gameObject.SetActive(false);menuCamera.enabled=true;
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
        void OnDestroy() { Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
    }
}

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
        GameObject matchRoot;
        Camera menuCamera;
        [SerializeField] GameObject reporterPrefab;
        public GameObject ReporterPrefab => reporterPrefab;

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
            var lightObject=new GameObject("PrototypeSun");
            lightObject.transform.SetParent(transform,false);
            var sun=lightObject.AddComponent<Light>();
            sun.type=LightType.Directional;
            sun.intensity=1.3f;
            sun.transform.rotation=Quaternion.Euler(50,-30,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.45f,.49f,.56f);
            RenderSettings.fog=false;
            gameObject.AddComponent<PrototypeHud>().Initialize(this);
        }

        void Start() { if(!Session) { Cursor.lockState=CursorLockMode.None;Cursor.visible=true; } }
        public void Report(string message) { Status=message; }
        public static void ValidateMap(BuildingData data)
        {
            data.Validate();StairLayout.Validate(data);
            if(data.floors.SelectMany(f=>f.items).Count(i=>i.type==ItemType.StreamerPC)!=1)
                throw new InvalidOperationException("游玩地图必须有且仅有一个主播电脑桌。");
        }
        public void StartSingle(TestRole role)
        {
            Lan.Disconnect();DisposeMatch();Role=role;RuntimeSettings=null;EnterPlay();
        }
        public void StartNetworkSession(BuildingData data,MatchSettings rules,TestRole role,bool authority)
        {
            DisposeMatch();Role=role;
            RuntimeSettings=JsonUtility.FromJson<MatchSettings>(JsonUtility.ToJson(rules));
            CreateMatch(data.Copy(),true,authority);
        }

        public void EnterPlay()
        {
            if(Session) return;
            RuntimeSettings=null;
            CreateMatch(buildingAsset?buildingAsset.CreateRuntimeCopy():BuildingData.CreateDefault(),false,true);
        }
        void CreateMatch(BuildingData data,bool networked,bool authority)
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
                Session.Initialize(this,spawn,networked,authority);
                Report(Role==TestRole.Streamer
                    ? "主播测试：空格 / 左键拍翅；达到目标分数后，WASD 搜捕。Tab 调试，Esc 退出。"
                    : "WASD 行走 · Shift 静步 · Ctrl 蹲下 · E 交互 · F 手机 · 左键/空格拍照 · Tab 调试。");
            }
            catch(Exception e)
            {
                DisposeMatch();
                StartupError="无法开始测试："+e.Message;
                Report(StartupError);
                Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            }
        }

        void DisposeMatch()
        {
            if(matchRoot) { matchRoot.SetActive(false); PrototypeVisuals.Release(matchRoot); }
            matchRoot=null; Session=null;
        }
        public void StopMatchForLobby()
        {
            DisposeMatch();RuntimeSettings=null;StartupError=null;
            World.gameObject.SetActive(false);menuCamera.enabled=true;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public void ReturnToMenu() { Lan.Disconnect();StopMatchForLobby(); }

        public void RestartGame() { if(Session && Session.IsNetwork) Lan.ReturnToLobby();else { DisposeMatch();EnterPlay(); } }
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

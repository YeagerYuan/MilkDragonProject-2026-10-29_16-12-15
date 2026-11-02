using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    // Instructional decoration only: never changes match rules, lighting or collision.
    public sealed class TutorialSigns : MonoBehaviour
    {
        sealed class Label
        {
            public TextMesh mesh;
            public Transform root;
            public float width,height;
        }
        readonly List<Label> labels=new List<Label>();
        MatchSession match;
        Font font;
        Material textMaterial;
        public int SignCount => labels.Count;

        public void Initialize(MatchSession session)
        {
            match=session;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Microsoft YaHei UI","SimHei","Arial"},64);
            textMaterial=new Material(Resources.Load<Shader>("HallText"));
            Font.textureRebuilt+=RefreshFont;
            CreateHallSigns();
        }
        void CreateHallSigns()
        {
            foreach(var item in match.World.Items.Where(i=>i.Data.type!=ItemType.Wall))
            {
                string text;
                switch(item.Data.type)
                {
                    case ItemType.Door: text=match.IsNetwork?"交互门\n两位玩家：看向门按 E\n打开 / 关闭，另一方同步可见":"交互门\n看向门按 E 打开 / 关闭\n练习进出房间";break;
                    case ItemType.Bed: text="床 / 藏身点\n狗仔：看向床按 E 躲藏\n再按 E 离开\n主播：按 E 翻查床铺\n大厅不抓捕、不扣分";break;
                    case ItemType.Furniture: text=item.IsHidingPlace?"衣柜 / 藏身点\n狗仔：看向衣柜按 E 躲藏\n再按 E 离开\n主播：按 E 翻查\n大厅不抓捕、不扣分":"家具 / 遮挡物\n练习绕行、蹲下和遮挡\nWASD 移动 · 按住 Ctrl 蹲下";break;
                    case ItemType.StreamerPC: text="主播电脑\n主播：靠近电脑按 E 体验交互\n大厅不启动小游戏\n正式对局才进行直播挑战";break;
                    case ItemType.QuestItem: text="拍照练习物品\n狗仔：F 打开 / 收起手机\n左键或空格拍照\n大厅只练习，没有任务或计分";break;
                    case ItemType.Stairs: text="折返楼梯\n直接走上 / 走下\n不需要按 E\n二楼也是练习区域";break;
                    default:text=item.DisplayName+"\n自由观察和碰撞练习";break;
                }
                Vector3 position=item.transform.position+new Vector3(item.Data.type==ItemType.Door?-2.7f:2.4f,2.2f,item.Data.type==ItemType.Door?-1.15f:-.6f);
                if(item.Data.type==ItemType.Furniture && !item.IsHidingPlace) position+=Vector3.back*2;
                Sign(position,text,2.3f);
            }
            Sign(new Vector3(-6,2.4f,-8.7f),"欢迎来到教学大厅\nWASD 移动 · Shift 静步 · Ctrl 蹲下\n"+(match.IsNetwork
                ? "两位玩家可以自由练习物体交互\nTab 打开大厅菜单，双方再次准备\n由房主开始正式对局"
                : "自由练习物体交互，没有倒计时\nTab 打开大厅菜单，准备进入对局\n再点击开始正式 AI 对战"),4.8f,180);
        }
        void Sign(Vector3 position,string text,float width,float yaw=0)
        {
            var root=new GameObject("说明牌 "+text.Split('\n')[0]).transform;
            root.SetParent(transform,false);root.position=position;root.rotation=Quaternion.Euler(0,yaw,0);
            int lines=text.Split('\n').Length;float height=lines*.27f+.24f;
            match.World.Palette.Box(root,"Board",Vector3.down*height*.5f,new Vector3(width,height,.08f),new Color(.055f,.13f,.18f),"UA_Props",false);
            if(match.IsTraining)
                match.World.Palette.Box(root,"Stand",new Vector3(0,-position.y*.5f,0),new Vector3(.07f,position.y,.07f),new Color(.25f,.32f,.36f),"UA_Props",false);
            var label=new GameObject("说明文字").AddComponent<TextMesh>();
            label.transform.SetParent(root,false);label.transform.localPosition=new Vector3(-width*.5f+.12f,-.12f,-.05f);
            label.font=font;label.fontSize=64;label.characterSize=.035f;label.anchor=TextAnchor.UpperLeft;label.lineSpacing=1.1f;label.color=Color.white;label.text=text;
            label.GetComponent<MeshRenderer>().sharedMaterial=textMaterial;
            labels.Add(new Label{mesh=label,root=root,width=width-.24f,height=height-.24f});
            RefreshFont(font);
        }
        void RefreshFont(Font changed) { if(changed==font && textMaterial) textMaterial.mainTexture=font.material.mainTexture; }
        void LateUpdate()
        {
            foreach(var label in labels)
            {
                if(!label.mesh || !label.root.gameObject.activeSelf) continue;
                var size=label.mesh.GetComponent<MeshRenderer>().localBounds.size;
                if(size.x>.001f && size.y>.001f) label.mesh.transform.localScale=Vector3.one*Mathf.Min(1,label.width/size.x,label.height/size.y);
            }
        }
        void OnDestroy()
        {
            Font.textureRebuilt-=RefreshFont;
            if(font) PrototypeVisuals.Release(font);
            if(textMaterial) PrototypeVisuals.Release(textMaterial);
        }
    }
}

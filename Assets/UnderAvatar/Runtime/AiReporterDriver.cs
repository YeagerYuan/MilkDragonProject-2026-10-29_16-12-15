using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnderAvatar
{
    // Uses the same controller, photo validation, doors and physical stair waypoints as the player.
    public sealed class AiReporterDriver : MonoBehaviour
    {
        public string Status { get; private set; }="寻找物证";
        public bool OnStairs => stairRoute!=null;
        MatchSession session;
        ReporterController reporter;
        BuildingNavigation navigation;
        List<Vector3Int> path;
        List<Vector3> stairRoute;
        int pathIndex,stairIndex;
        Vector3? pendingDestination;
        Vector3? photoTarget;
        PlacedItemView hidingPlace;
        float nextDecision,hiddenUntil;
        bool sneaking;
        public bool HasPath => path!=null && pathIndex<path.Count;

        public void Initialize(MatchSession match)
        {
            session=match; reporter=match.Reporter; navigation=new BuildingNavigation(match.World.Data);
        }
        public bool SetDestination(Vector3 destination)
        {
            if(OnStairs) { pendingDestination=destination; return true; }
            var next=navigation.FindPath(transform.position,destination);
            if(next==null) return false;
            path=next; pathIndex=0;
            if(path.Count>1 && Vector3.Distance(transform.position,navigation.Position(path[0]))<.15f) pathIndex=1;
            return true;
        }
        void Update()
        {
            if(session==null || session.Finished) return;
            if(reporter.Hidden)
            {
                Status="藏在衣柜中";
                if(Time.time>=hiddenUntil || session.StreamerPhotoAvailable)
                { reporter.Interact(); hiddenUntil=Time.time+1; nextDecision=0; }
                return;
            }
            if(Time.time>=nextDecision && !OnStairs)
            {
                nextDecision=Time.time+.45f;
                Decide();
            }
            if(session.Finished || reporter.Hidden) return;
            Move(Time.deltaTime);
        }
        bool SeesThreat()
        {
            var actor=session.Streamer;
            if(actor.State==StreamerState.Streaming || actor.Floor!=reporter.Floor) return false;
            Vector3 delta=actor.PhotoPoint-reporter.View.transform.position;
            return delta.magnitude<7 && Vector3.Angle(reporter.View.transform.forward,delta)<85 &&
                !Physics.Linecast(reporter.View.transform.position,actor.PhotoPoint,PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore);
        }
        void Decide()
        {
            if(SeesThreat())
            {
                photoTarget=null; sneaking=false; reporter.SetCrouched(false);
                var wardrobe=session.World.Items.Where(i=>i.Data.hideable && i.Floor==reporter.Floor &&
                    Vector3.Distance(i.Front,transform.position)<8 &&
                    Vector3.Distance(i.Front,session.Streamer.transform.position)>2)
                    .OrderBy(i=>Vector3.Distance(i.Front,transform.position)).FirstOrDefault();
                if(wardrobe)
                {
                    hidingPlace=wardrobe; SetDestination(wardrobe.Front); Status="寻找衣柜躲藏";
                }
                else
                {
                    hidingPlace=null;
                    var safe=navigation.ReachableFrom(transform.position).Where(c=>c.y==reporter.Floor &&
                        Vector3.Distance(navigation.Position(c),transform.position)<9)
                        .OrderByDescending(c=>Vector3.Distance(navigation.Position(c),session.Streamer.transform.position)).FirstOrDefault();
                    SetDestination(navigation.Position(safe)); Status="躲避主播";
                }
                return;
            }
            if(hidingPlace)
            {
                if(Vector3.Distance(transform.position,hidingPlace.Front)<1.5f &&
                    !Physics.Linecast(reporter.View.transform.position,hidingPlace.Front+Vector3.up,PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore))
                {
                    reporter.Hide(hidingPlace); hiddenUntil=Time.time+7; hidingPlace=null; path=null; return;
                }
                if(HasPath) return;
                hidingPlace=null;
            }
            if(photoTarget.HasValue && !HasPath)
            {
                reporter.SetLookAt(photoTarget.Value); reporter.SetPhone(true);
                Physics.SyncTransforms();
                if(session.HasValidTarget) { session.TakePhoto(); photoTarget=null; return; }
                photoTarget=null;
            }
            if(HasPath) return;
            // Live status is visible on the phone; the AI does not track a roaming streamer through walls.
            if(session.StreamerPhotoAvailable && PlanPhoto(session.Streamer.PhotoPoint,session.Streamer.Floor))
            { Status="接近电脑，拍摄直播"; sneaking=true; return; }
            foreach(var item in session.World.Items.Where(i=>i.Data.type==ItemType.QuestItem &&
                session.Quests.Any(q=>q.Id==i.Data.questId && !q.Complete))
                .OrderBy(i=>Vector3.Distance(i.transform.position,transform.position)))
            {
                if(PlanPhoto(item.PhotoPoint,item.Floor))
                { Status="收集任务物证"; sneaking=false; return; }
            }
            reporter.SetPhone(false); sneaking=true;
            var hide=session.World.Items.Where(i=>i.Data.hideable).OrderBy(i=>Vector3.Distance(i.Front,transform.position)).FirstOrDefault();
            if(hide && SetDestination(hide.Front)) { hidingPlace=hide; Status="等待下一场直播"; }
        }
        bool PlanPhoto(Vector3 point,int floor)
        {
            float maxDistance=Mathf.Min(session.Settings.maxPhotoDistance-.3f,6);
            var candidates=navigation.ReachableFrom(transform.position).Where(c=>c.y==floor)
                .Select(navigation.Position)
                .Where(p=>Vector3.Distance(p+Vector3.up*1.65f,point)>1.8f &&
                    Vector3.Distance(p+Vector3.up*1.65f,point)<maxDistance &&
                    Vector3.Distance(p,transform.position)>.35f)
                .OrderBy(p=>Vector3.Distance(p,transform.position));
            foreach(var candidate in candidates)
            {
                Vector3 eye=candidate+Vector3.up*1.65f, ray=point-eye;
                if(Physics.Raycast(eye,ray.normalized,ray.magnitude-.03f,PrototypeLayers.PhotoMask,QueryTriggerInteraction.Ignore)) continue;
                if(SetDestination(candidate)) { photoTarget=point; return true; }
            }
            return false;
        }
        void Move(float dt)
        {
            Vector3? target=null;
            if(OnStairs) target=stairRoute[stairIndex];
            else if(HasPath)
            {
                var cell=path[pathIndex];
                if(pathIndex>0 && cell.y!=path[pathIndex-1].y)
                {
                    var stair=navigation.StairBetween(path[pathIndex-1],cell);
                    if(stair==null) path=null;
                    else { stairRoute=stair.Waypoints(cell.y>path[pathIndex-1].y); stairIndex=0; target=stairRoute[0]; }
                }
                else
                {
                    var item=session.World.Data.At(cell.y,cell.x,cell.z);
                    if(item?.type==ItemType.Door)
                    {
                        var door=session.World.Find(item.id);
                        if(!door.IsOpen)
                        {
                            door.SetDoorOpen(true); Physics.SyncTransforms();
                            session.EmitNoise(door.transform.position,NoiseKind.Door);
                        }
                    }
                    target=navigation.Position(cell);
                }
            }
            if(!target.HasValue) { reporter.MoveInput(Vector2.zero,false,dt); return; }
            Vector3 delta=target.Value-transform.position; delta.y=0;
            if(delta.magnitude<.09f && Mathf.Abs(target.Value.y-transform.position.y)<.4f)
            {
                if(OnStairs)
                {
                    stairIndex++;
                    if(stairIndex>=stairRoute.Count)
                    {
                        stairRoute=null; pathIndex++;
                        if(pendingDestination.HasValue)
                        { var destination=pendingDestination.Value; pendingDestination=null; SetDestination(destination); }
                    }
                }
                else pathIndex++;
                reporter.MoveInput(Vector2.zero,sneaking,dt); return;
            }
            if(delta.sqrMagnitude>.0001f) reporter.SetLookAt(transform.position+delta.normalized*3+Vector3.up*1.4f);
            Vector3 local=transform.InverseTransformDirection(delta);
            float magnitude=Mathf.Min(1,delta.magnitude/(reporter.MovementSpeed(sneaking)*Mathf.Max(.001f,dt)));
            reporter.MoveInput(new Vector2(local.x,local.z).normalized*magnitude,sneaking,dt);
        }
    }
}

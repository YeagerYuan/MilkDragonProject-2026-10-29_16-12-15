#include "ShellPrototype.h"
#include "Camera/CameraComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Components/CapsuleComponent.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Engine/Canvas.h"
#include "Engine/Engine.h"
#include "Engine/World.h"
#include "EngineUtils.h"
#include "InputKeyEventArgs.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Kismet/GameplayStatics.h"
#include "DrawDebugHelpers.h"
#include "Misc/CommandLine.h"
#include "Misc/Parse.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "TimerManager.h"
#include "UnrealClient.h"

namespace
{
    const TCHAR* Names[] = {TEXT("Grass tile"), TEXT("Sand tile"), TEXT("Raised ground"), TEXT("Hen point"),
        TEXT("Fence"), TEXT("Rock"), TEXT("Tree"), TEXT("Player spawn")};
    const FLinearColor Cream(.95f,.80f,.48f), Red(.8f,.07f,.035f), Wood(.30f,.12f,.045f);
    const TCHAR* Slot = TEXT("ShelljackIsland");
    void TestKey(APlayerController* PC, FKey Key, EInputEvent Event)
    {
        PC->InputKey(FInputKeyEventArgs(nullptr,FInputDeviceId::CreateFromInternalId(0),Key,Event,uint64(0)));
    }
}

AShellPiece::AShellPiece()
{
    RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
    RootComponent->SetMobility(EComponentMobility::Movable);
}
void AShellPiece::OnConstruction(const FTransform& Transform) { Super::OnConstruction(Transform); Rebuild(); }
void AShellPiece::Part(const TCHAR* Shape, FVector P, FVector S, FLinearColor Color, FRotator R)
{
    auto* Mesh = NewObject<UStaticMeshComponent>(this);
    Mesh->SetupAttachment(RootComponent);
    Mesh->SetMobility(EComponentMobility::Movable);
    Mesh->SetStaticMesh(LoadObject<UStaticMesh>(nullptr, *FString::Printf(TEXT("/Engine/BasicShapes/%s.%s"), Shape, Shape)));
    Mesh->SetRelativeLocation(P); Mesh->SetRelativeScale3D(S); Mesh->SetRelativeRotation(R);
    Mesh->SetCollisionProfileName(TEXT("BlockAll"));
    Mesh->SetCollisionEnabled(bPreview ? ECollisionEnabled::NoCollision : ECollisionEnabled::QueryAndPhysics);
    if (!bPreview && Kind == EShellPiece::Spawn)
    {
        Mesh->SetCollisionEnabled(ECollisionEnabled::QueryOnly);
        Mesh->SetCollisionResponseToAllChannels(ECR_Ignore);
        Mesh->SetCollisionResponseToChannel(ECC_Visibility,ECR_Block);
    }
    auto* Base = LoadObject<UMaterialInterface>(nullptr, TEXT("/Game/Prototype/M_Prototype.M_Prototype"));
    if (Base)
    {
        auto* Mat = UMaterialInstanceDynamic::Create(Base, this);
        Mat->SetVectorParameterValue(TEXT("Tint"), bPreview ? FLinearColor(.15f,.75f,.65f) : Color);
        Mesh->SetMaterial(0, Mat);
    }
    Mesh->RegisterComponent(); Parts.Add(Mesh);
}
void AShellPiece::Rebuild()
{
    for (auto Mesh : Parts) if (Mesh) Mesh->DestroyComponent();
    Parts.Empty();
    switch (Kind)
    {
    case EShellPiece::Grass:
        Part(TEXT("Cube"),{0,0,-35},{4,4,.7},{.23f,.42f,.12f}); break;
    case EShellPiece::Sand:
        Part(TEXT("Cube"),{0,0,-35},{4,4,.7},{.65f,.49f,.27f}); break;
    case EShellPiece::Platform:
        Part(TEXT("Cube"),{0,0,90},{4,4,1.8},{.32f,.24f,.13f});
        Part(TEXT("Cube"),{0,0,185},{4,4,.1},{.28f,.47f,.15f}); break;
    case EShellPiece::Fence:
        for (float X : {-140.f,140.f}) Part(TEXT("Cube"),{X,0,65},{.2,.25,1.3},Wood);
        for (float Z : {45.f,100.f}) Part(TEXT("Cube"),{0,0,Z},{3,.16,.16},{.48f,.25f,.09f});
        break;
    case EShellPiece::Hen:
        Part(TEXT("Cylinder"),{0,0,8},{3.3,3.3,.16},{.42f,.24f,.08f});
        Part(TEXT("Sphere"),{0,0,135},{2.1,1.7,2.2},Cream);
        Part(TEXT("Sphere"),{55,0,245},{1.15,1.1,1.2},Cream);
        for (float Y : {-77.f,77.f}) Part(TEXT("Sphere"),{-12,Y,140},{1.1,.32,1.2},{.83f,.57f,.25f});
        for (float X : {25.f,55.f,85.f}) Part(TEXT("Sphere"),{X,0,303},{.40,.28,.65},Red);
        Part(TEXT("Cone"),{122,0,244},{.5,.5,.7},{1.f,.48f,.02f},{90,0,0});
        Part(TEXT("Sphere"),{100,0,210},{.35,.3,.5},Red);
        for (float Y : {-48.f,48.f})
        {
            Part(TEXT("Sphere"),{83,Y,260},{.19,.12,.19},{.02f,.025f,.03f});
            Part(TEXT("Cylinder"),{20,Y,40},{.15,.15,.6},{.8f,.38f,.02f});
        }
        for (float Y : {-70.f,0.f,70.f}) Part(TEXT("Sphere"),{130,Y,28},{.35,.28,.45},{.3f,.7f,.65f});
        break;
    case EShellPiece::Rock:
        Part(TEXT("Sphere"),{0,0,48},{1.8,1.5,1.25},{.37f,.40f,.39f},{8,25,13}); break;
    case EShellPiece::Tree:
        Part(TEXT("Cylinder"),{0,0,155},{.35,.35,3.1},Wood);
        Part(TEXT("Cone"),{0,0,310},{2.8,2.8,2.8},{.12f,.29f,.13f});
        Part(TEXT("Cone"),{0,0,420},{2.0,2.0,2.2},{.21f,.41f,.17f}); break;
    case EShellPiece::Spawn:
        Part(TEXT("Cylinder"),{0,0,4},{1.1,1.1,.08},{.10f,.65f,.95f});
        Part(TEXT("Cone"),{40,0,70},{.4,.4,1.0},{.10f,.65f,.95f},{90,0,0}); break;
    }
}
void AShellPiece::SetPreview(bool Value) { bPreview=Value; Rebuild(); }
void AShellPiece::Highlight(bool Value)
{
    for (auto Mesh : Parts) if (Mesh) Mesh->SetRenderCustomDepth(Value);
}

AShellCharacter::AShellCharacter()
{
    GetCapsuleComponent()->InitCapsuleSize(34,88);
    Camera=CreateDefaultSubobject<UCameraComponent>(TEXT("FirstPersonCamera"));
    Camera->SetupAttachment(GetCapsuleComponent()); Camera->SetRelativeLocation({0,0,64});
    Camera->bUsePawnControlRotation=true; Camera->FieldOfView=90;
    GetCharacterMovement()->MaxWalkSpeed=450;
    GetCharacterMovement()->JumpZVelocity=500;
    GetCharacterMovement()->AirControl=.25;
    GetCharacterMovement()->MaxStepHeight=45;
    bUseControllerRotationYaw=true;
}
AShellGameMode::AShellGameMode()
{
    PlayerControllerClass=AShellController::StaticClass();
    HUDClass=AShellHUD::StaticClass();
    DefaultPawnClass=nullptr;
}
AShellController::AShellController() { PrimaryActorTick.bCanEverTick=true; }
void AShellController::Message(const FString& Text) { Status=Text; StatusUntil=GetWorld()->GetTimeSeconds()+6; }
AShellPiece* AShellController::Add(EShellPiece Kind, FVector L, FRotator R, FVector S)
{
    auto* A=GetWorld()->SpawnActor<AShellPiece>(L,R);
    A->Kind=Kind; A->SetActorScale3D(S); A->Rebuild(); return A;
}
void AShellController::BeginPlay()
{
    Super::BeginPlay();
    bSmokeTest=FParse::Param(FCommandLine::Get(),TEXT("ShellSmokeTest"));
    Builder=GetWorld()->SpawnActor<APawn>();
    auto* Root=NewObject<USceneComponent>(Builder); Builder->SetRootComponent(Root); Root->RegisterComponent();
    auto* Cam=NewObject<UCameraComponent>(Builder); Cam->SetupAttachment(Root); Cam->bUsePawnControlRotation=true; Cam->RegisterComponent();
    Builder->SetActorLocation({2200,-2600,2200});
    Possess(Builder); SetControlRotation(FRotator(-32,130,0));
    bShowMouseCursor=true; SetInputMode(FInputModeGameAndUI().SetHideCursorDuringCapture(false));
    if (PieceCount()==0) MakeDemo();
    const bool bCapture=FParse::Param(FCommandLine::Get(),TEXT("ShellCapture"));
    if (!bSmokeTest && !bCapture && UGameplayStatics::DoesSaveGameExist(Slot,0)) LoadLayout();
    Choose(0);
    Message(TEXT("Welcome! Choose a piece, click to place. TAB = walk / build."));
    if (bSmokeTest) GetWorldTimerManager().SetTimerForNextTick(this,&AShellController::RunSmokeTest);
    if (bCapture)
    {
        FTimerHandle T1,T2,T3,T4;
        GetWorldTimerManager().SetTimer(T1,[this](){
            FScreenshotRequest::RequestScreenshot(FPaths::ProjectSavedDir()/TEXT("Builder.png"),true,false);
        },6.f,false);
        GetWorldTimerManager().SetTimer(T2,[this](){TogglePlay();SetControlRotation({0,65,0});},9.f,false);
        GetWorldTimerManager().SetTimer(T3,[this](){
            FScreenshotRequest::RequestScreenshot(FPaths::ProjectSavedDir()/TEXT("FirstPerson.png"),true,false);
        },12.f,false);
        GetWorldTimerManager().SetTimer(T4,[this](){ConsoleCommand(TEXT("quit"),true);},15.f,false);
    }
}
int32 AShellController::PieceCount() const
{
    int32 Count=0;
    for(TActorIterator<AShellPiece> It(GetWorld());It;++It) if(!It->bPreview) ++Count;
    return Count;
}
void AShellController::MakeDemo()
{
    for(int32 X=-4;X<=4;++X) for(int32 Y=-4;Y<=4;++Y)
        Add((FMath::Abs(X)==4 || FMath::Abs(Y)==4)?EShellPiece::Sand:EShellPiece::Grass,{X*400.,Y*400.,0});
    Add(EShellPiece::Hen,{600,500,0},FRotator(0,-90,0));
    for(int32 I=0;I<5;++I) Add(EShellPiece::Fence,{-600.+I*300,1000,0});
    for(FVector P : {FVector(-1200,800,0),FVector(-1000,1200,0),FVector(1400,1000,0),FVector(1200,-800,0)})
        Add(EShellPiece::Tree,P);
    Add(EShellPiece::Rock,{-800,-700,0});
    Add(EShellPiece::Rock,{1100,100,0},FRotator(0,40,0),{1.4,1.2,1.0});
    Add(EShellPiece::Platform,{-1000,0,0});
    Add(EShellPiece::Spawn,{0,-800,0},FRotator(0,90,0));
}
TArray<FShellRecord> AShellController::Snapshot() const
{
    TArray<FShellRecord> R;
    for(TActorIterator<AShellPiece> It(GetWorld());It;++It) if(!It->bPreview)
    { FShellRecord P; P.Kind=It->Kind; P.Transform=It->GetActorTransform(); R.Add(P); }
    return R;
}
bool AShellController::ValidateRecords(const TArray<FShellRecord>& R) const
{
    if(R.Num()>2000) return false;
    for(const auto& P:R)
    {
        const FVector S=P.Transform.GetScale3D(), L=P.Transform.GetLocation();
        if(static_cast<uint8>(P.Kind)>7 || P.Transform.ContainsNaN() || L.GetAbsMax()>50000 ||
            S.GetMin()<.25 || S.GetMax()>4) return false;
    }
    return true;
}
void AShellController::Restore(const TArray<FShellRecord>& R)
{
    Cancel(); Select(nullptr);
    TArray<AShellPiece*> Old;
    for(TActorIterator<AShellPiece> It(GetWorld());It;++It) if(!It->bPreview) Old.Add(*It);
    for(auto* A:Old) A->Destroy();
    for(const auto& P:R) Add(P.Kind,P.Transform.GetLocation(),P.Transform.Rotator(),P.Transform.GetScale3D());
}
void AShellController::RecordUndo()
{
    UndoStack.Add(Snapshot()); if(UndoStack.Num()>32) UndoStack.RemoveAt(0);
    RedoStack.Empty();
}
void AShellController::Undo(bool bRedo)
{
    if(bPlaying) return;
    Cancel();
    auto& From=bRedo?RedoStack:UndoStack;
    auto& To=bRedo?UndoStack:RedoStack;
    if(From.IsEmpty()) {Message(TEXT("No more history."));return;}
    To.Add(Snapshot()); const auto R=From.Pop(); Restore(R); Message(bRedo?TEXT("Redone."):TEXT("Undone."));
}
void AShellController::SaveLayout()
{
    if(bPlaying) return;
    Cancel();
    auto* Save=Cast<UShellSave>(UGameplayStatics::CreateSaveGameObject(UShellSave::StaticClass()));
    Save->Pieces=Snapshot();
    Message(UGameplayStatics::SaveGameToSlot(Save,Slot,0)?TEXT("Island saved to Saved/SaveGames/ShelljackIsland.sav"):TEXT("Save failed."));
}
void AShellController::LoadLayout()
{
    if(bPlaying) return;
    auto* Save=Cast<UShellSave>(UGameplayStatics::LoadGameFromSlot(Slot,0));
    if(!Save || Save->Version!=1 || !ValidateRecords(Save->Pieces)) {Message(TEXT("No valid saved island found."));return;}
    Cancel(); RecordUndo(); Restore(Save->Pieces); Message(TEXT("Saved island loaded."));
}
void AShellController::Select(AShellPiece* P)
{
    if(IsValid(Selected)) Selected->Highlight(false);
    Selected=P;
    if(IsValid(P)) {P->Highlight(true); Yaw=P->GetActorRotation().Yaw; Size=P->GetActorScale3D().X;}
}
void AShellController::Cancel()
{
    if(bMoving && IsValid(Selected)) Selected->SetActorTransform(BeforeMove);
    bMoving=false;
}
void AShellController::Choose(int32 V)
{
    Cancel(); Select(nullptr); Tool=FMath::Clamp(V,0,8); Height=0; Size=1; Yaw=0;
    if(IsValid(Preview)) Preview->Destroy(); Preview=nullptr;
    if(Tool>0)
    {
        Preview=Add(static_cast<EShellPiece>(Tool-1),FVector::ZeroVector);
        Preview->SetPreview(true);
    }
}
FString AShellController::ToolName() const {return Tool==0?TEXT("Select / edit"):Names[Tool-1];}
bool AShellController::IsOverUI(float X,float Y) const
{
    int32 W,H; GetViewportSize(W,H);
    return X<270 || Y<76 || Y>H-85;
}
void AShellController::CursorTrace()
{
    float MX,MY;
    bCursorValid=false;
    if(!GetMousePosition(MX,MY)||IsOverUI(MX,MY)||IsInputKeyDown(EKeys::RightMouseButton))
    {if(Preview) Preview->SetActorHiddenInGame(true);return;}
    FVector O,D; if(!DeprojectMousePositionToWorld(O,D)) return;
    FCollisionQueryParams Q; Q.AddIgnoredActor(Builder);
    if(Preview) Q.AddIgnoredActor(Preview); if(bMoving && Selected) Q.AddIgnoredActor(Selected);
    FHitResult Hit;
    if(GetWorld()->LineTraceSingleByChannel(Hit,O,O+D*50000,ECC_Visibility,Q)) CursorPosition=Hit.ImpactPoint;
    else if(D.Z<-.01) CursorPosition=O+D*(-O.Z/D.Z);
    else return;
    if(CursorPosition.GetAbsMax()>45000) return;
    if(bSnap) {CursorPosition.X=FMath::GridSnap(CursorPosition.X,100.);CursorPosition.Y=FMath::GridSnap(CursorPosition.Y,100.);}
    CursorPosition.Z+=Height; bCursorValid=true;
    if(Preview)
    {
        Preview->SetActorHiddenInGame(false);
        Preview->SetActorLocation(CursorPosition); Preview->SetActorRotation({0,Yaw,0}); Preview->SetActorScale3D(FVector(Size));
    }
    if(bMoving && Selected) Selected->SetActorLocation(CursorPosition);
}
void AShellController::LeftClick()
{
    float X,Y; if(!GetMousePosition(X,Y)) return;
    if(auto* H=Cast<AShellHUD>(GetHUD())) if(H->Click(X,Y)) return;
    if(IsOverUI(X,Y)) return;
    if(bMoving && Selected)
    {
        if(!bCursorValid) return;
        const FTransform Final=Selected->GetActorTransform();
        Selected->SetActorTransform(BeforeMove); RecordUndo(); Selected->SetActorTransform(Final);
        bMoving=false; Message(TEXT("Moved. G to move again.")); return;
    }
    if(Tool>0 && bCursorValid)
    {
        if(PieceCount()>=2000) {Message(TEXT("Prototype limit: 2000 pieces."));return;}
        RecordUndo();
        if(Tool==8)
        {
            TArray<AShellPiece*> Spawns;
            for(TActorIterator<AShellPiece> It(GetWorld());It;++It) if(!It->bPreview&&It->Kind==EShellPiece::Spawn) Spawns.Add(*It);
            for(auto* A:Spawns) A->Destroy();
        }
        Add(static_cast<EShellPiece>(Tool-1),CursorPosition,{0,Yaw,0},FVector(Size)); return;
    }
    FHitResult Hit; GetHitResultUnderCursor(ECC_Visibility,false,Hit);
    Select(Cast<AShellPiece>(Hit.GetActor()));
}
void AShellController::RemoveSelected()
{
    if(IsValid(Selected)) {Cancel();RecordUndo();Selected->Destroy();Selected=nullptr;Message(TEXT("Deleted. Ctrl+Z to undo."));}
}
void AShellController::Rotate()
{
    if(bMoving) return;
    Yaw=FMath::Fmod(Yaw+15,360);
    if(Tool==0&&IsValid(Selected)) {RecordUndo();Selected->SetActorRotation({0,Yaw,0});}
}
void AShellController::Resize(float Delta)
{
    if(bMoving) return;
    Size=FMath::Clamp(Size+Delta,.25f,4.f);
    if(Tool==0&&IsValid(Selected)) {RecordUndo();Selected->SetActorScale3D(FVector(Size));}
}
void AShellController::Elevate(float Delta)
{
    if(bMoving || Tool>0) Height=FMath::Clamp(Height+Delta,-1000.f,3000.f);
    else if(IsValid(Selected)) {RecordUndo();Selected->AddActorWorldOffset({0,0,Delta});}
}
void AShellController::Respawn()
{
    FVector L(0,-800,200); FRotator R(0,90,0);
    for(TActorIterator<AShellPiece> It(GetWorld());It;++It) if(!It->bPreview&&It->Kind==EShellPiece::Spawn)
    {L=It->GetActorLocation()+FVector(0,0,100);R=It->GetActorRotation();break;}
    FHitResult Hit; FCollisionQueryParams Q; Q.AddIgnoredActor(Walker);
    if(GetWorld()->LineTraceSingleByChannel(Hit,L+FVector(0,0,300),L-FVector(0,0,1500),ECC_Visibility,Q))
        L.Z=Hit.ImpactPoint.Z+100;
    for(int32 I=0;I<12;++I)
    {
        FVector Try=L+FVector(0,0,I*100);
        if(GetWorld()->FindTeleportSpot(Walker,Try,R))
        {Walker->SetActorLocationAndRotation(Try,R,false,nullptr,ETeleportType::TeleportPhysics);break;}
    }
    SetControlRotation(R); Walker->GetCharacterMovement()->StopMovementImmediately();
}
void AShellController::TogglePlay()
{
    Cancel();
    if(!bPlaying)
    {
        Select(nullptr); EditorPosition=Builder->GetActorLocation(); EditorRotation=GetControlRotation();
        if(Preview) Preview->SetActorHiddenInGame(true);
        if(!Walker) Walker=GetWorld()->SpawnActor<AShellCharacter>(FVector(0,0,2000),FRotator::ZeroRotator);
        if(!Walker) {Message(TEXT("Could not create player."));return;}
        bPlaying=true; Possess(Walker); Respawn();
        bShowMouseCursor=false; SetInputMode(FInputModeGameOnly());
    }
    else
    {
        bPlaying=false; Possess(Builder); Builder->SetActorLocation(EditorPosition); SetControlRotation(EditorRotation);
        if(Walker) {Walker->Destroy();Walker=nullptr;}
        bShowMouseCursor=true; SetInputMode(FInputModeGameAndUI().SetHideCursorDuringCapture(false));
        Message(TEXT("Back in builder. Your island is unchanged."));
    }
}
void AShellController::Action(int32 Id)
{
    if(Id>=0&&Id<=8) Choose(Id);
    else if(Id==20) TogglePlay();
    else if(Id==21) SaveLayout();
    else if(Id==22) LoadLayout();
    else if(Id==23) Undo(false);
    else if(Id==24) Undo(true);
    else if(Id==25) bSnap=!bSnap;
    else if(Id==26) {if(Selected&&!bMoving){BeforeMove=Selected->GetActorTransform();bMoving=true;Height=0;}}
    else if(Id==27) Rotate();
    else if(Id==28) RemoveSelected();
    else if(Id==29) Resize(-.25f);
    else if(Id==30) Resize(.25f);
    else if(Id==31) Elevate(-50);
    else if(Id==32) Elevate(50);
}
bool AShellController::InputKey(const FInputKeyEventArgs& P)
{
    const bool Handled=Super::InputKey(P);
    if(P.Event==IE_Released&&P.Key==EKeys::SpaceBar&&Walker) Walker->StopJumping();
    if(P.Event!=IE_Pressed) return Handled;
    if(P.Key==EKeys::Tab) {TogglePlay();return true;}
    if(bPlaying)
    {
        if(P.Key==EKeys::SpaceBar&&Walker) Walker->Jump();
        if(P.Key==EKeys::Home) Respawn();
        if(P.Key==EKeys::Escape) TogglePlay();
        return Handled;
    }
    const FKey Keys[]={EKeys::Zero,EKeys::One,EKeys::Two,EKeys::Three,EKeys::Four,EKeys::Five,EKeys::Six,EKeys::Seven,EKeys::Eight};
    for(int32 I=0;I<9;++I) if(P.Key==Keys[I]) {Choose(I);return true;}
    if(P.Key==EKeys::LeftMouseButton) LeftClick();
    if(P.Key==EKeys::Escape) {Cancel();Choose(0);}
    if(P.Key==EKeys::R) Rotate();
    if(P.Key==EKeys::G) Action(26);
    if(P.Key==EKeys::Delete) RemoveSelected();
    if(P.Key==EKeys::H) bSnap=!bSnap;
    if(P.Key==EKeys::Equals||P.Key==EKeys::Add) Resize(.25);
    if(P.Key==EKeys::Hyphen||P.Key==EKeys::Subtract) Resize(-.25);
    if(P.Key==EKeys::PageUp) Elevate(50);
    if(P.Key==EKeys::PageDown) Elevate(-50);
    if(P.Key==EKeys::F5) SaveLayout();
    if(P.Key==EKeys::F9) LoadLayout();
    if(IsInputKeyDown(EKeys::LeftControl)||IsInputKeyDown(EKeys::RightControl))
    {if(P.Key==EKeys::Z) Undo(false);if(P.Key==EKeys::Y) Undo(true);}
    if(P.Key==EKeys::F&&Selected)
    {
        const FVector Target=Selected->GetActorLocation();
        Builder->SetActorLocation(Target+FVector(650,-650,650));SetControlRotation({-32,135,0});
    }
    if(P.Key==EKeys::MouseScrollUp) Builder->AddActorWorldOffset(GetControlRotation().Vector()*180);
    if(P.Key==EKeys::MouseScrollDown) Builder->AddActorWorldOffset(GetControlRotation().Vector()*-180);
    return Handled;
}
void AShellController::PlayerTick(float Dt)
{
    Super::PlayerTick(Dt);
    if(!Builder) return;
    float DX,DY; GetInputMouseDelta(DX,DY);
    const bool Look=bPlaying||IsInputKeyDown(EKeys::RightMouseButton);
    if(Look)
    {
        FRotator R=GetControlRotation(); R.Yaw+=DX*.16;R.Pitch=FMath::Clamp(FRotator::NormalizeAxis(R.Pitch)-DY*.16,-85.,85.);
        SetControlRotation(R);
    }
    const float Forward=(IsInputKeyDown(EKeys::W)?1.f:0.f)-(IsInputKeyDown(EKeys::S)?1.f:0.f);
    const float Side=(IsInputKeyDown(EKeys::D)?1.f:0.f)-(IsInputKeyDown(EKeys::A)?1.f:0.f);
    if(bPlaying&&Walker)
    {
        const FRotator R(0,GetControlRotation().Yaw,0);
        Walker->AddMovementInput(R.Vector(),Forward);
        Walker->AddMovementInput(FRotationMatrix(R).GetUnitAxis(EAxis::Y),Side);
        Walker->GetCharacterMovement()->MaxWalkSpeed=IsInputKeyDown(EKeys::LeftShift)?750:450;
        if(Walker->GetActorLocation().Z < -1800) Respawn();
    }
    else
    {
        if(Look)
        {
            const float Up=(IsInputKeyDown(EKeys::E)?1.f:0.f)-(IsInputKeyDown(EKeys::Q)?1.f:0.f);
            FVector Move=GetControlRotation().Vector()*Forward+FRotationMatrix(GetControlRotation()).GetUnitAxis(EAxis::Y)*Side+FVector::UpVector*Up;
            Builder->AddActorWorldOffset(Move.GetClampedToMaxSize(1)*Dt*(IsInputKeyDown(EKeys::LeftShift)?2400:1100));
        }
        CursorTrace();
        if(Selected)
        {
            FVector Origin,Extent; Selected->GetActorBounds(false,Origin,Extent);
            DrawDebugBox(GetWorld(),Origin,Extent+FVector(3),FColor(255,193,71),false,-1,0,3);
        }
    }
}

void AShellHUD::Button(const FString& Label,float X,float Y,float W,int32 Id,bool Active)
{
    float MX=0,MY=0; GetOwningPlayerController()->GetMousePosition(MX,MY);
    const bool Hover=MX>=X&&MX<X+W&&MY>=Y&&MY<Y+32;
    DrawRect(Active?FLinearColor(.08,.42,.34,.98):(Hover?FLinearColor(.16,.23,.26,.98):FLinearColor(.075,.11,.14,.98)),X,Y,W,32);
    DrawText(Label,FLinearColor::White,X+10,Y+7,GEngine->GetSmallFont(),1);
    Buttons.Add({{X,Y},{W,32},Id});
}
bool AShellHUD::Click(float X,float Y)
{
    for(const auto& B:Buttons) if(X>=B.Position.X&&X<B.Position.X+B.Size.X&&Y>=B.Position.Y&&Y<B.Position.Y+B.Size.Y)
    {if(auto* P=Cast<AShellController>(GetOwningPlayerController())) P->Action(B.Id);return true;}
    return false;
}
void AShellHUD::DrawHUD()
{
    Super::DrawHUD(); Buttons.Empty();
    auto* P=Cast<AShellController>(GetOwningPlayerController()); if(!P||!Canvas) return;
    const float W=Canvas->SizeX,H=Canvas->SizeY;
    DrawRect({.022,.037,.045,.96},0,0,W,70);
    DrawText(TEXT("SHELLJACK / ISLAND LAB"),{.68,.91,.78},22,13,GEngine->GetMediumFont(),.85);
    DrawText(P->bPlaying?TEXT("FIRST PERSON  /  single player"):TEXT("BUILDER  /  prototype 0.1"),FLinearColor::White,22,43,GEngine->GetSmallFont());
    if(P->bPlaying)
    {
        DrawRect(FLinearColor::White,W*.5-2,H*.5-2,4,4);
        DrawRect({.02,.04,.05,.85},0,H-54,W,54);
        DrawText(TEXT("WASD Move   Mouse Look   Space Jump   Shift Sprint   Home Respawn   TAB Return to builder"),FLinearColor::White,22,H-34,GEngine->GetSmallFont());
        return;
    }
    Button(TEXT("PLAY [Tab]"),300,18,130,20,true);
    Button(TEXT("SAVE [F5]"),440,18,125,21);
    Button(TEXT("LOAD [F9]"),575,18,125,22);
    Button(TEXT("UNDO"),710,18,85,23);Button(TEXT("REDO"),805,18,85,24);
    DrawRect({.025,.042,.05,.96},0,70,260,H-70);
    DrawText(TEXT("PLACE OBJECTS"),{.55,.68,.7},20,88,GEngine->GetSmallFont());
    Button(TEXT("0  Select / edit"),16,112,228,0,P->Tool==0);
    for(int32 I=0;I<8;++I) Button(FString::Printf(TEXT("%d  %s"),I+1,Names[I]),16,151+I*38,228,I+1,P->Tool==I+1);
    Button(P->bSnap?TEXT("GRID 100cm [H]: ON"):TEXT("GRID [H]: OFF"),16,463,228,25,P->bSnap);
    Button(TEXT("Move [G]"),16,505,108,26); Button(TEXT("Rotate [R]"),134,505,110,27);
    Button(TEXT("Scale -"),16,544,108,29);Button(TEXT("Scale +"),134,544,110,30);
    Button(TEXT("Lower [PgDn]"),16,583,108,31);Button(TEXT("Raise [PgUp]"),134,583,110,32);
    Button(TEXT("Delete selected [Del]"),16,625,228,28);
    DrawRect({.022,.037,.045,.94},270,H-80,W-270,80);
    DrawText(FString::Printf(TEXT("%s  |  Pieces: %d  |  Rotation: %.0f  Scale: %.2f  Height: %.0f"),*P->ToolName(),P->PieceCount(),P->Yaw,P->Size,P->Height),
        {.75,.9,.8},286,H-72,GEngine->GetSmallFont());
    DrawText(TEXT("Right mouse + WASD: fly | Q/E: down/up | Wheel: zoom | F: focus | Ctrl+Z/Y: undo/redo"),
        FLinearColor::White,286,H-47,GEngine->GetSmallFont());
    DrawText(P->bMoving?TEXT("MOVING: click to confirm, Escape to cancel"):TEXT("Click: place/select | R: turn 15 degrees | +/-: scale | PgUp/PgDn: height"),
        FLinearColor::White,286,H-25,GEngine->GetSmallFont());
    if(GetWorld()->GetTimeSeconds()<P->StatusUntil)
    {
        DrawRect({.04,.12,.12,.94},286,88,FMath::Min(W-305,900.f),34);
        DrawText(P->Status,{.8,1,.9},298,97,GEngine->GetSmallFont());
    }
}

void AShellController::RunSmokeTest()
{
    bool Good=true;
    auto Check=[&Good](bool Pass,const TCHAR* Name){Good&=Pass;UE_LOG(LogTemp,Display,TEXT("SHELLTEST %s: %s"),Name,Pass?TEXT("PASS"):TEXT("FAIL"));};
    const auto Original=Snapshot(); const int32 Count=PieceCount();
    Check(Count>80,TEXT("starter island"));
    RecordUndo();auto* P=Add(EShellPiece::Fence,{0,0,0});
    Check(PieceCount()==Count+1&&P->Parts.Num()==4,TEXT("placement"));
    Undo(false);Check(PieceCount()==Count,TEXT("undo"));
    Undo(true);Check(PieceCount()==Count+1,TEXT("redo"));
    Choose(0);for(TActorIterator<AShellPiece> It(GetWorld());It;++It) if(!It->bPreview&&It->Kind==EShellPiece::Hen){Select(*It);break;}
    const FTransform Old=Selected->GetActorTransform();
    Action(26);Selected->SetActorLocation({200,200,300});Cancel();
    Check(Selected->GetActorTransform().Equals(Old),TEXT("cancel move"));
    Rotate();Resize(.25);Check(Selected->GetActorScale3D().Equals(FVector(1.25)),TEXT("transform"));
    auto* Save=Cast<UShellSave>(UGameplayStatics::CreateSaveGameObject(UShellSave::StaticClass()));Save->Pieces=Snapshot();
    Check(UGameplayStatics::SaveGameToSlot(Save,TEXT("ShelljackSmokeTest"),0),TEXT("save"));
    auto* Loaded=Cast<UShellSave>(UGameplayStatics::LoadGameFromSlot(TEXT("ShelljackSmokeTest"),0));
    Check(Loaded&&ValidateRecords(Loaded->Pieces)&&Loaded->Pieces.Num()==Save->Pieces.Num(),TEXT("load"));
    bool Same=Loaded&&Loaded->Pieces.Num()==Save->Pieces.Num();
    if(Same) for(int32 I=0;I<Save->Pieces.Num();++I)
        Same &= Loaded->Pieces[I].Kind==Save->Pieces[I].Kind && Loaded->Pieces[I].Transform.Equals(Save->Pieces[I].Transform);
    Check(Same,TEXT("saved types and transforms"));
    UGameplayStatics::DeleteGameInSlot(TEXT("ShelljackSmokeTest"),0);
    FHitResult Hit;Check(GetWorld()->LineTraceSingleByChannel(Hit,{0,0,500},{0,0,-500},ECC_Visibility),TEXT("ground collision"));
    TogglePlay();Check(bPlaying&&Walker&&GetPawn()==Walker,TEXT("first person possession"));
    TogglePlay();Check(!bPlaying&&GetPawn()==Builder,TEXT("return to builder"));
    Restore(Original);
    TestKey(this,EKeys::Tab,IE_Pressed);TestKey(this,EKeys::Tab,IE_Released);
    FTimerHandle SettleTimer;
    GetWorldTimerManager().SetTimer(SettleTimer,[this,Good]() mutable {
        const bool Landed=Walker&&Walker->GetCharacterMovement()->IsMovingOnGround();
        Good&=Landed;
        UE_LOG(LogTemp,Display,TEXT("SHELLTEST gravity and landing: %s"),Landed?TEXT("PASS"):TEXT("FAIL"));
        const FVector Start=Walker->GetActorLocation();
        TestKey(this,EKeys::W,IE_Pressed);
        FTimerHandle MoveTimer;
        GetWorldTimerManager().SetTimer(MoveTimer,[this,Good,Start]() mutable {
            const bool Moved=FVector::Dist2D(Start,Walker->GetActorLocation())>150;
            Good&=Moved;
            UE_LOG(LogTemp,Display,TEXT("SHELLTEST walking movement: %s"),Moved?TEXT("PASS"):TEXT("FAIL"));
            TestKey(this,EKeys::W,IE_Released);
            const float JumpZ=Walker->GetActorLocation().Z; TestKey(this,EKeys::SpaceBar,IE_Pressed);
            FTimerHandle JumpTimer;
            GetWorldTimerManager().SetTimer(JumpTimer,[this,Good,JumpZ]() mutable {
                const bool Jumped=Walker->GetActorLocation().Z>JumpZ+50;
                Good&=Jumped;
                UE_LOG(LogTemp,Display,TEXT("SHELLTEST jump: %s"),Jumped?TEXT("PASS"):TEXT("FAIL"));
                TestKey(this,EKeys::SpaceBar,IE_Released);
                Walker->SetActorLocation({0,-800,-2000});
                FTimerHandle FallTimer;
                GetWorldTimerManager().SetTimer(FallTimer,[this,Good]() mutable {
                    const bool Recovered=Walker->GetActorLocation().Z>0;
                    Good&=Recovered;
                    UE_LOG(LogTemp,Display,TEXT("SHELLTEST fall recovery: %s"),Recovered?TEXT("PASS"):TEXT("FAIL"));
                    TestKey(this,EKeys::W,IE_Pressed);
                    FTimerHandle WallTimer;
                    GetWorldTimerManager().SetTimer(WallTimer,[this,Good]() mutable {
                        TestKey(this,EKeys::W,IE_Released);
                        const float Y=Walker->GetActorLocation().Y;
                        const bool Blocked=Y>850 && Y<990;
                        Good &= Blocked;
                        UE_LOG(LogTemp,Display,TEXT("SHELLTEST fence blocks walking: %s (Y=%.1f)"),Blocked?TEXT("PASS"):TEXT("FAIL"),Y);
                        TestKey(this,EKeys::Tab,IE_Pressed);TestKey(this,EKeys::Tab,IE_Released);
                        Good &= !bPlaying&&GetPawn()==Builder;
                        FFileHelper::SaveStringToFile(Good?TEXT("PASS"):TEXT("FAIL"),*(FPaths::ProjectSavedDir()/TEXT("SmokeTest.txt")));
                        ConsoleCommand(TEXT("quit"),true);
                    },5.f,false);
                },.5f,false);
            },.22f,false);
        },.6f,false);
    },.6f,false);
}


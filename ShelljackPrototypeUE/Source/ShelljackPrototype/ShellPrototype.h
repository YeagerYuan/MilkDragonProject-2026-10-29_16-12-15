#pragma once
#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "GameFramework/Character.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/GameModeBase.h"
#include "GameFramework/HUD.h"
#include "GameFramework/SaveGame.h"
#include "ShellPrototype.generated.h"

UENUM(BlueprintType, meta=(ScriptName="ShellPieceKind"))
enum class EShellPiece : uint8 { Grass, Sand, Platform, Hen, Fence, Rock, Tree, Spawn };

USTRUCT()
struct FShellRecord
{
    GENERATED_BODY()
    UPROPERTY(SaveGame) EShellPiece Kind = EShellPiece::Grass;
    UPROPERTY(SaveGame) FTransform Transform;
};

UCLASS()
class UShellSave : public USaveGame
{
    GENERATED_BODY()
public:
    UPROPERTY(SaveGame) int32 Version = 1;
    UPROPERTY(SaveGame) TArray<FShellRecord> Pieces;
};

UCLASS(Blueprintable)
class AShellPiece : public AActor
{
    GENERATED_BODY()
public:
    AShellPiece();
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Shelljack") EShellPiece Kind = EShellPiece::Grass;
    UPROPERTY(Transient) bool bPreview = false;
    UPROPERTY(Transient) TArray<TObjectPtr<class UStaticMeshComponent>> Parts;
    virtual void OnConstruction(const FTransform& Transform) override;
    void Rebuild();
    void SetPreview(bool Value);
    void Highlight(bool Value);
private:
    void Part(const TCHAR* Shape, FVector Position, FVector Scale, FLinearColor Color, FRotator Rotation = FRotator::ZeroRotator);
};

UCLASS()
class AShellCharacter : public ACharacter
{
    GENERATED_BODY()
public:
    AShellCharacter();
    UPROPERTY() TObjectPtr<class UCameraComponent> Camera;
};

UCLASS()
class AShellController : public APlayerController
{
    GENERATED_BODY()
public:
    AShellController();
    virtual void BeginPlay() override;
    virtual void PlayerTick(float DeltaTime) override;
    virtual bool InputKey(const struct FInputKeyEventArgs& Params) override;
    void Action(int32 Id);
    void Choose(int32 Value);
    void TogglePlay();
    void SaveLayout();
    void LoadLayout();
    void Undo(bool bRedo);
    void MakeDemo();
    TArray<FShellRecord> Snapshot() const;
    void Restore(const TArray<FShellRecord>& Records);
    bool ValidateRecords(const TArray<FShellRecord>& Records) const;
    void Message(const FString& Text);
    int32 PieceCount() const;
    FString ToolName() const;
    bool IsOverUI(float X, float Y) const;
    UPROPERTY() TObjectPtr<APawn> Builder;
    UPROPERTY() TObjectPtr<AShellCharacter> Walker;
    UPROPERTY() TObjectPtr<AShellPiece> Preview;
    UPROPERTY() TObjectPtr<AShellPiece> Selected;
    bool bPlaying = false;
    bool bMoving = false;
    bool bSnap = true;
    bool bCursorValid = false;
    int32 Tool = 0;
    float Yaw = 0;
    float Height = 0;
    float Size = 1;
    FVector CursorPosition = FVector::ZeroVector;
    FString Status;
    double StatusUntil = 0;
    bool bSmokeTest = false;
private:
    TArray<TArray<FShellRecord>> UndoStack;
    TArray<TArray<FShellRecord>> RedoStack;
    FTransform BeforeMove;
    FVector EditorPosition;
    FRotator EditorRotation;
    void RecordUndo();
    void LeftClick();
    void Cancel();
    void Select(AShellPiece* Piece);
    void RemoveSelected();
    void Rotate();
    void Resize(float Delta);
    void Elevate(float Delta);
    void CursorTrace();
    AShellPiece* Add(EShellPiece Kind, FVector Location, FRotator Rotation = FRotator::ZeroRotator, FVector Scale = FVector::OneVector);
    void Respawn();
    void RunSmokeTest();
};

UCLASS()
class AShellHUD : public AHUD
{
    GENERATED_BODY()
public:
    virtual void DrawHUD() override;
    struct FButton { FVector2D Position, Size; int32 Id; };
    TArray<FButton> Buttons;
    bool Click(float X, float Y);
private:
    void Button(const FString& Label, float X, float Y, float W, int32 Id, bool Active = false);
};

UCLASS()
class AShellGameMode : public AGameModeBase
{
    GENERATED_BODY()
public:
    AShellGameMode();
};


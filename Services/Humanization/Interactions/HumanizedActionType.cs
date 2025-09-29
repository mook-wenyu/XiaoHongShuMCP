using System.Text.Json.Serialization;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：支持的拟人化动作类型。
/// English: Supported humanized interaction action types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HumanizedActionType
{
    Unknown = 0,
    Hover,
    Click,
    DoubleClick,
    ContextClick,
    Drag,
    DragAndDrop,
    MoveRandom,
    Wheel,
    ScrollTo,
    InputText,
    PressKey,
    Hotkey,
    SelectOption,
    UploadFile,
    IdlePause,
    WaitFor,
    Evaluate,
    Custom
}

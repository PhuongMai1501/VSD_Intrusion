namespace CameraManager.Messaging
{
    public sealed class MessageDispatchResult
    {
        public bool Success { get; init; }
        public string Summary { get; init; } = string.Empty;
    }
}

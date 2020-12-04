namespace StackX.Flow
{
    internal interface ILoggingFlowElementDecorator
    {
        void SetLogging(bool enable);
        bool IsLoggingEnabled { get; }
    }
}
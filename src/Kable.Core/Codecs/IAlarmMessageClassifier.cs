namespace Kable.Codecs;

/// <summary>Optional protocol-owned alarm classification. Sessions do not infer domain meaning from ToString().</summary>
public interface IAlarmMessageClassifier<in TMessage>
{
    bool IsAlarmMessage(TMessage message);
}

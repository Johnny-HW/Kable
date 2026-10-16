namespace Kable.Localization;

using System;
using System.Collections.Generic;

public sealed partial class KableLocalizer
{
    private void InitializeFrenchTranslations()
    {
        _errorDictionary["fr"] = new Dictionary<KableErrorCode, string>
        {
            [KableErrorCode.None] = "Succès.",
            [KableErrorCode.DeviceDisconnected] = "La connexion matérielle a été interrompue. (Annulation immédiate de toutes les requêtes)",
            [KableErrorCode.DeviceTimeout] = "La commande '{0}' a expiré après {1}ms.",
            [KableErrorCode.ProtocolViolation] = "Violation de protocole détectée: {0}",
            [KableErrorCode.InvalidCast] = "Type de réponse attendu '{0}', mais reçu '{1}'.",
            [KableErrorCode.ConnectionFailed] = "Échec de l'établissement de la connexion avec '{0}'.",
            [KableErrorCode.OperationCanceled] = "L'opération a été annulée.",
            [KableErrorCode.BufferOverflow] = "Capacité du tampon dépassée.",
            [KableErrorCode.HeartbeatLost] = "Signal de heartbeat perdu. Connexion considérée comme rompue.",
            [KableErrorCode.HardwareFault] = "Panne matérielle détectée: {0}"
        };
    }
}

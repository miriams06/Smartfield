using SmartField.Domain.Enums;

namespace SmartField.Application.Attendance;

public static class AttendanceSequenceRules
{
    public static IReadOnlyList<AttendanceEventType> GetAllowedNextEventTypes(
        AttendanceEventType? lastEventType)
    {
        return lastEventType switch
        {
            null => [AttendanceEventType.ClockIn],
            AttendanceEventType.ClockIn =>
            [
                AttendanceEventType.BreakStart,
                AttendanceEventType.ClockOut
            ],
            AttendanceEventType.BreakStart => [AttendanceEventType.BreakEnd],
            AttendanceEventType.BreakEnd =>
            [
                AttendanceEventType.BreakStart,
                AttendanceEventType.ClockOut
            ],
            AttendanceEventType.ClockOut => [AttendanceEventType.ClockIn],
            _ => []
        };
    }

    public static bool IsAllowed(
        AttendanceEventType? lastEventType,
        AttendanceEventType nextEventType)
    {
        return GetAllowedNextEventTypes(lastEventType).Contains(nextEventType);
    }

    public static string GetCurrentState(AttendanceEventType? lastEventType)
    {
        return lastEventType switch
        {
            null => "NoRecord",
            AttendanceEventType.ClockIn => "Working",
            AttendanceEventType.BreakStart => "OnBreak",
            AttendanceEventType.BreakEnd => "Working",
            AttendanceEventType.ClockOut => "Closed",
            _ => "Unknown"
        };
    }

    public static string GetCurrentStateLabel(AttendanceEventType? lastEventType)
    {
        return lastEventType switch
        {
            null => "SEM REGISTO",
            AttendanceEventType.ClockIn => "EM TRABALHO",
            AttendanceEventType.BreakStart => "EM PAUSA",
            AttendanceEventType.BreakEnd => "EM TRABALHO",
            AttendanceEventType.ClockOut => "DIA FECHADO",
            _ => "ESTADO DESCONHECIDO"
        };
    }

    public static string BuildSequenceError(
        AttendanceEventType? lastEventType,
        AttendanceEventType nextEventType)
    {
        var allowedLabels = GetAllowedNextEventTypes(lastEventType)
            .Select(GetDisplayName)
            .ToArray();
        var allowedMessage = allowedLabels.Length == 0
            ? "Neste momento não há ações disponíveis."
            : $"Neste momento podes: {JoinLabels(allowedLabels)}.";

        return $"Não é possível {GetDisplayName(nextEventType).ToLowerInvariant()} agora. {allowedMessage}";
    }

    public static string GetDisplayName(AttendanceEventType eventType)
    {
        return eventType switch
        {
            AttendanceEventType.ClockIn => "Registar entrada",
            AttendanceEventType.BreakStart => "Iniciar pausa",
            AttendanceEventType.BreakEnd => "Terminar pausa",
            AttendanceEventType.ClockOut => "Registar saída",
            _ => eventType.ToString()
        };
    }

    private static string JoinLabels(IReadOnlyList<string> labels)
    {
        return labels.Count switch
        {
            0 => string.Empty,
            1 => labels[0],
            2 => $"{labels[0]} ou {labels[1]}",
            _ => $"{string.Join(", ", labels.Take(labels.Count - 1))} ou {labels[^1]}"
        };
    }
}

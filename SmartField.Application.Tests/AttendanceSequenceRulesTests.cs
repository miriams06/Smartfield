using SmartField.Application.Attendance;
using SmartField.Domain.Enums;

namespace SmartField.Application.Tests;

public class AttendanceSequenceRulesTests
{
    [Theory]
    [InlineData(null, new[] { AttendanceEventType.ClockIn })]
    [InlineData(
        AttendanceEventType.ClockIn,
        new[] { AttendanceEventType.BreakStart, AttendanceEventType.ClockOut })]
    [InlineData(AttendanceEventType.BreakStart, new[] { AttendanceEventType.BreakEnd })]
    [InlineData(
        AttendanceEventType.BreakEnd,
        new[] { AttendanceEventType.BreakStart, AttendanceEventType.ClockOut })]
    [InlineData(AttendanceEventType.ClockOut, new[] { AttendanceEventType.ClockIn })]
    public void GetAllowedNextEventTypes_ReturnsExpectedOperations(
        AttendanceEventType? lastEventType,
        AttendanceEventType[] expected)
    {
        var allowed = AttendanceSequenceRules.GetAllowedNextEventTypes(lastEventType);

        Assert.Equal(expected, allowed);
    }

    [Fact]
    public void IsAllowed_DoesNotAllowClockOutWithoutClockIn()
    {
        var isAllowed = AttendanceSequenceRules.IsAllowed(
            null,
            AttendanceEventType.ClockOut);

        Assert.False(isAllowed);
    }

    [Fact]
    public void IsAllowed_DoesNotAllowBreakStartWithoutClockIn()
    {
        var isAllowed = AttendanceSequenceRules.IsAllowed(
            null,
            AttendanceEventType.BreakStart);

        Assert.False(isAllowed);
    }

    [Fact]
    public void IsAllowed_DoesNotAllowBreakEndWithoutBreakStart()
    {
        var isAllowed = AttendanceSequenceRules.IsAllowed(
            AttendanceEventType.ClockIn,
            AttendanceEventType.BreakEnd);

        Assert.False(isAllowed);
    }

    [Fact]
    public void IsAllowed_DoesNotAllowTwoClockInsInARow()
    {
        var isAllowed = AttendanceSequenceRules.IsAllowed(
            AttendanceEventType.ClockIn,
            AttendanceEventType.ClockIn);

        Assert.False(isAllowed);
    }

    [Fact]
    public void IsAllowed_AllowsMultipleBreaksAfterBreakEnd()
    {
        var isAllowed = AttendanceSequenceRules.IsAllowed(
            AttendanceEventType.BreakEnd,
            AttendanceEventType.BreakStart);

        Assert.True(isAllowed);
    }

    [Theory]
    [InlineData(null, "NoRecord")]
    [InlineData(AttendanceEventType.ClockIn, "Working")]
    [InlineData(AttendanceEventType.BreakStart, "OnBreak")]
    [InlineData(AttendanceEventType.BreakEnd, "Working")]
    [InlineData(AttendanceEventType.ClockOut, "Closed")]
    public void GetCurrentState_ReturnsExpectedState(
        AttendanceEventType? lastEventType,
        string expected)
    {
        var state = AttendanceSequenceRules.GetCurrentState(lastEventType);

        Assert.Equal(expected, state);
    }

    [Fact]
    public void BuildSequenceError_UsesUserFriendlyLabelsAndAllowedActions()
    {
        var message = AttendanceSequenceRules.BuildSequenceError(
            AttendanceEventType.BreakEnd,
            AttendanceEventType.BreakEnd);

        Assert.Equal(
            "Não é possível terminar pausa agora. Neste momento podes: Iniciar pausa ou Registar saída.",
            message);
    }
}

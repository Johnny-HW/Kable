namespace Kable.OpcUa.Tests;

using System;
using Kable.OpcUa.Models;
using Xunit;

public class OpcUaModelTests
{
    [Fact]
    public void OpcUaDataValue_PropertyInitialization_PreservesValuesAndTypes()
    {
        string nodeId = "ns=2;s=Device.Chamber1.Pressure";
        double expectedValue = 101.325;
        uint statusCode = 0; // Good

        var dataValue = new OpcUaDataValue(nodeId, expectedValue, statusCode);

        Assert.Equal(nodeId, dataValue.NodeId);
        Assert.Equal(expectedValue, dataValue.Value);
        Assert.Equal(statusCode, dataValue.StatusCode);
        Assert.True(dataValue.IsGood);
        Assert.Equal(101.325, dataValue.GetValue<double>());
    }

    [Fact]
    public void OpcUaDataValue_BadStatusCode_ReportsNotGood()
    {
        string nodeId = "ns=2;s=Device.Sensor.Error";
        uint badStatusCode = 0x80000000; // Bad status bit

        var dataValue = new OpcUaDataValue(nodeId, null, badStatusCode);

        Assert.False(dataValue.IsGood);
        Assert.Null(dataValue.Value);
    }
}

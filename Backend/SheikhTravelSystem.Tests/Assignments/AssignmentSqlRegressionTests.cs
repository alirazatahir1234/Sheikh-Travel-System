namespace SheikhTravelSystem.Tests.Assignments;

public class AssignmentSqlRegressionTests
{
    [Fact]
    public void AssignmentSql_ListSelect_UsesMaintenanceDate_NotScheduledDate()
    {
        var source = ReadInfrastructureSql("AssignmentSql.cs");
        Assert.Contains("MaintenanceDate", source);
        Assert.DoesNotContain("ScheduledDate", source);
    }

    [Fact]
    public void AssignmentSql_ListFrom_UsesGpsRecordedAt_NotTimestampColumn()
    {
        var source = ReadInfrastructureSql("AssignmentSql.cs");
        Assert.Contains("RecordedAt AS Timestamp", source);
        Assert.DoesNotContain("ORDER BY p.Timestamp", source);
    }

    [Fact]
    public void AssignmentValidation_UsesMaintenanceDate_NotScheduledDate()
    {
        var source = ReadInfrastructureRepository("AssignmentRepository.cs");
        Assert.Contains("MaintenanceDate", source);
        Assert.DoesNotContain("ScheduledDate", source);
    }

    [Fact]
    public void AssignmentValidation_TreatsDriverAndVehicleConflictsAsWarnings()
    {
        // Create/transfer complete prior Active rows — conflicts must not hard-block canProceed.
        var repo = ReadInfrastructureRepository("AssignmentRepository.cs");
        var sql = ReadInfrastructureSql("AssignmentValidationSql.cs");
        foreach (var source in new[] { repo, sql })
        {
            Assert.Contains("DriverConflict", source);
            Assert.Contains("VehicleConflict", source);
            Assert.Contains(
                "Driver already has another active assignment. Continuing will complete it and transfer the driver.",
                source);
            Assert.DoesNotContain(
                """issues.Add(new("DriverConflict", "Driver already has another active assignment.", "Error"));""",
                source);
            Assert.DoesNotContain(
                """issues.Add(new("VehicleConflict", ex.Message, "Error"));""",
                source);
        }
    }

    [Fact]
    public void MaintenanceReportQueries_DoesNotReferenceScheduleName()
    {
        var source = ReadInfrastructureRepository("MaintenanceModuleRepository.cs");
        Assert.DoesNotContain("ScheduleName", source);
        Assert.Contains("ServiceTypeName", source);
    }

    [Fact]
    public void MaintenanceReportQueries_VendorPerformanceUsesPartUsageUsedAt()
    {
        var source = ReadInfrastructureRepository("MaintenanceModuleRepository.cs");
        Assert.Contains("pu.UsedAt", source);
        Assert.DoesNotContain("pu.CreatedAt", source);
    }

    private static string ReadInfrastructureSql(string fileName)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "SheikhTravelSystem.Infrastructure", "Persistence", "Repositories", "Sql", fileName);
        return File.ReadAllText(Path.GetFullPath(dir));
    }

    private static string ReadInfrastructureRepository(string fileName)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "SheikhTravelSystem.Infrastructure", "Persistence", "Repositories", fileName);
        return File.ReadAllText(Path.GetFullPath(dir));
    }
}

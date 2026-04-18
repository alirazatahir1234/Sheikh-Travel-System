using System.Data;

namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}

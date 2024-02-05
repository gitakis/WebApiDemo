# WebApiDemo
ASP.NET core api with authentication and logging

Installation:

1. Install SQL Server
2. Set sql server connection string in file .\WebApiDemo\appsettings.json
	property path: ConnectionStrings.DefaultConnection = Server=<server name>;Database=<database name>;TrustServerCertificate=True;User ID=<user name>;Password=<password>;

	Example:
	  "ConnectionStrings": {
	    "DefaultConnection": "Server=test;Database=test_database;TrustServerCertificate=True;User ID=sa;Password=test;"
	  }
3. Open solution with Visual Studio 2022
	3.1 Create database schema. Open Package Manager Console and Run command: update-database
	3.2 Run api service with CTRL+F5

4. Logs are written to file .\WebApiDemo\Logs\applog-20240205


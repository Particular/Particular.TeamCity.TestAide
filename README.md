# Particular.TeamCity.TestAide
Console app that executes .NET Core and .NET 5 and .NET 6 tests within TeamCity

## Parameters
All parameters are required

| Parameter | Valid Input | TeamCity Value |
| --------- | ----------- | -------------- |
| `-gvmaj` or `--gitversionmajor` | integer | `GitVersion.Major` |
| `-gvmin` or `--gitversionminor` | integer | `GitVersion.Minor` |
| `-ncmaj` or `--netcoreversionmajor` | integer | `VersionThatStartedNetCoreSupport` |
| `-ncmin` or `--netcoreversionminor` | integer | `MinorVersionThatStartedNetCoreSupport` |
| `-curdir` or `--currentdirectory` | string | |
| `-udep` or `--unixdependencies` | only string values `true` or `false` | |

module.exports = {
  ci: {
    collect: {
      startServerCommand: "dotnet run --project SilverSet/SilverSet.csproj --urls http://localhost:5295",
      url: ["http://localhost:5295/"],
      numberOfRuns: 1,
      startServerReadyTimeout: 60000
    },
    upload: {
      target: "temporary-public-storage"
    }
  }
};

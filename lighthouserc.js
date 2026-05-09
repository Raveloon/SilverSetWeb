module.exports = {
  ci: {
    collect: {
      startServerCommand: "dotnet run --project SilverSet/SilverSet.csproj --no-build --configuration Release --urls http://localhost:5295",
      url: ["http://localhost:5295/"],
      numberOfRuns: 1,
      startServerReadyPattern: "hosting environment", // "Now listening on" yerine daha erken tetiklenir
      startServerReadyTimeout: 120000,
      settings: {
        chromeFlags: "--no-sandbox --headless --disable-gpu"
      }
    },
    upload: {
      target: "temporary-public-storage"
    }
  }
};
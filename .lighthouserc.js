module.exports = {
    ci: {
        collect: {
            // Projeyi GitHub Actions üzerinde ayağa kaldıracak komut
            startServerCommand: "dotnet run --project SilverSet/SilverSet.csproj",
            // Sunucunun tamamen açılmasını beklemek için URL
            url: [
                "http://localhost:5000/",
                "http://localhost:5000/Home/Index"
            ],
            numberOfRuns: 2,
            // Sunucunun hazır olduğundan emin olmak için bekleme süresi (ms)
            startServerReadyTimeout: 60000 
        },
        upload: {
            target: "temporary-public-storage"
        }
    }
};

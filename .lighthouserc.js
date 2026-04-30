module.exports = {
    ci: {
        collect: {
            url: [
                "http://localhost:5000/",
                "http://localhost:5000/Home/Index"
            ],
            numberOfRuns: 2
        },
        upload: {
            target: "temporary-public-storage"
        }
    }
};
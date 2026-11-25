# SilverSet Studios — Digital Asset Marketplace & Community Platform

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-MVC-22863a?logo=dotnet&logoColor=white)](https://learn.microsoft.com/aspnet/core)
[![Entity Framework Core](https://img.shields.io/badge/Entity%20Framework-Core%209-68217A?logo=csharp&logoColor=white)](https://learn.microsoft.com/ef/core/)
[![SignalR](https://img.shields.io/badge/SignalR-Real--Time%20Hub-orange?logo=signalr&logoColor=white)](https://learn.microsoft.com/aspnet/core/signalr)
[![Docker](https://img.shields.io/badge/Docker-Containerized-2496ED?logo=docker&logoColor=white)](https://www.docker.com/)
[![Accessibility](https://img.shields.io/badge/pa11y-WCAG%20AA%20Compliant-blue)](https://pa11y.org/)
[![Cloudflare Turnstile](https://img.shields.io/badge/Cloudflare-Turnstile%20Protected-F38020?logo=cloudflare&logoColor=white)](https://www.cloudflare.com/products/turnstile/)

A modern, high-performance web platform built with **ASP.NET Core 9 (MVC)** for digital creative asset distribution, community engagement, and customer support. Engineered with clean layered architecture, robust security standards, real-time communication via WebSockets, and high accessibility compliance (WCAG 2.1 AA).

---

## 🚀 Key Features

### 💎 1. Digital Asset Showcase & Marketplace
- Dynamic asset catalog with pagination, filtering, and responsive gallery layout.
- Detailed asset presentation with technical specifications, tags, licensing info, and preview media.
- Automated PDF asset specification generation using **DinkToPdf** (native `libwkhtmltox` integration).

### 💬 2. Community Forum & Discussion Hub
- Multi-category discussion threads with interactive replies and view tracking.
- **Automated Moderation & Profanity Filtering:** Integrated `ProfanityFilterService` with dictionary-based regex scanning to sanitize abusive language in real time.
- Custom service filters (`ForumAccessFilter`) enforcing community access controls and account status verification.

### ⚡ 3. Real-Time Support Ticket System
- Bidirectional live messaging between clients and support agents powered by **SignalR** (`TicketHub`).
- Attachment handling (`TicketAttachment`) for bug reports, screenshots, and crash logs.
- Status management (Open, Pending, Resolved, Closed) with administrative tracking.

### 🛡️ 4. Enterprise Security & Hardening
- **ASP.NET Core Identity:** Secure password hashing, account lockout policies, email confirmation, and brute-force protection.
- **Fixed-Window Rate Limiting:** Built-in middleware limiting requests to prevent DDoS and credential stuffing attacks on authentication endpoints.
- **Cloudflare Turnstile Integration:** Modern, frictionless CAPTCHA alternative protecting login, registration, and contact forms from automated bots.
- **Audit Logging:** Comprehensive tracking of security events, including user logins (`UserLoginLog`) and password resets (`PasswordResetLog`).
- **Honeypot Protection:** Hidden inputs on authentication forms to trap and deflect scraping bots.

### 📊 5. Administration Control Panel (Admin Area)
- Dedicated `Admin` area with RBAC (Role-Based Access Control) restrictions.
- User management, asset moderation, forum category configuration, and ticket resolution workflows.
- Dynamic global site configuration management (`GlobalSetting`).

---

## 🛠️ Architecture & Tech Stack

| Layer | Technology |
|---|---|
| **Framework** | .NET 9.0 (C# 13) |
| **Architecture** | ASP.NET Core MVC with Layered Separation (Areas, Services, Filters, Hubs, Entities) |
| **ORM / Data** | Entity Framework Core 9 (Code-First), MS SQL Server |
| **Real-Time** | Microsoft ASP.NET Core SignalR |
| **Security** | ASP.NET Core Identity, Cloudflare Turnstile, Microsoft RateLimiter |
| **Document Engine** | DinkToPdf (HTML-to-PDF conversion) |
| **Frontend** | Bootstrap 5, Modern Vanilla JS, CSS3 Custom Properties (Dark Theme & Neon Glow), Bootstrap Icons |
| **DevOps & QA** | Docker & Docker Compose, GitHub Actions CI/CD, Pa11y Accessibility Audits, Google Lighthouse CI |

---

## 📂 Project Structure

```
SilverSet/
├── SilverSet.sln
└── SilverSet/
    ├── Areas/
    │   └── Admin/                 # Administrative dashboard & management controllers
    ├── Controllers/               # MVC Controllers (Account, Assets, Forum, Contact, etc.)
    ├── Data/                      # DbContext, seed configurations, and migrations
    ├── Filters/                   # Action & Service filters (e.g. ForumAccessFilter)
    ├── Hubs/                      # SignalR WebSocket hubs (TicketHub)
    ├── Models/
    │   ├── Entities/              # Core domain entities (AppUser, Asset, Ticket, BadWord...)
    │   └── ViewModels/            # Request/Response ViewModels with DataAnnotations
    ├── Services/                  # Business logic (ProfanityFilter, Turnstile, ForumSettings)
    ├── Views/                     # Razor views and shared partials
    ├── wwwroot/                   # Static assets (custom styling, scripts, media)
    ├── Program.cs                 # Dependency injection, middleware pipeline, rate limiting
    ├── Dockerfile                 # Container build definition
    └── .pa11yci                   # Accessibility testing configuration
```

---

## ⚙️ Getting Started

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or higher
- [Microsoft SQL Server](https://www.microsoft.com/sql-server/) (LocalDB, Express, or Docker container)
- Visual Studio 2022 (v17.12+) or VS Code with C# Dev Kit

### Local Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/Raveloon/SilverSetWeb.git
   cd SilverSetWeb/SilverSet
   ```

2. **Configure Database Connection & Secrets:**
   Update your connection string in `appsettings.Development.json` or via `dotnet user-secrets`:
   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\mssqllocaldb;Database=SilverSetDb;Trusted_Connection=True;MultipleActiveResultSets=true"
   ```

3. **Apply Database Migrations:**
   ```bash
   dotnet ef database update
   ```

4. **Run the Application:**
   ```bash
   dotnet run
   ```
   Navigate to `https://localhost:5001` or `http://localhost:5000`.

---

## 🐳 Docker Deployment

The application includes multi-stage container build support:

```bash
docker build -t silversit-web:latest -f SilverSet/Dockerfile .
docker run -d -p 8080:8080 --name silversit silversit-web:latest
```

---

## ♿ Accessibility & Quality Standards

- Verified with **Pa11y CI** ensuring zero high-contrast or screen-reader violations across all public routes.
- Semantic HTML5 structure, ARIA labels, keyboard navigability, and automated Lighthouse audit integration.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

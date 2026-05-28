# Gym Management System

A premium, feature-rich, and modern **Gym Management System** built with **C# .NET WPF (Windows Presentation Foundation)** and **SQLite**. This application offers an all-in-one solution for gym owners, administrators, and staff to seamlessly manage members, instructors, attendance tracking, gym subscription plans, payments, fixed/promo discounts, refunds, and dynamic financial/attendance reporting.

Featuring a sleek, highly customizable dashboard interface with support for multiple themes (Light/Dark mode), custom accent colors, automatic backup/restore utilities, and automated attendance session management.

---

## Key Features

### Member Management
*   **Comprehensive Profiles:** Register and manage members with details like name, contact number, gender, birthday, member type, and join/expiry dates.
*   **Avatar & Photo Integration:** Capture or upload profile pictures for visual verification.
*   **Special Classifications:** Classify members (Regular, Student, Senior) to automatically apply appropriate pricing structures and fixed discounts.
*   **Assigned Instructors:** Link members to specific gym trainers/instructors for personalized training.

### Instructor & Trainer Management
*   **Instructor Profiles:** Store personal information, hire dates, contact details, and photos.
*   **Specializations & Certifications:** Log trainer qualifications across various disciplines (e.g., Strength & Conditioning, Cardio & Weight Loss, Yoga, HIIT, CrossFit, Bodybuilding).
*   **Client Tracking:** Instantly view all members currently assigned to a specific instructor.
*   **Attendance Checking:** Separate dedicated check-in/check-out logs for trainers.

### Gym Plans & Subscription Pipeline
*   **Flexible Tiered Pricing:** Configurable rates (Daily, Weekly, Half-Month, Monthly, Yearly).
*   **Custom Packages:** Add, edit, and archive custom membership rates with flexible duration days.
*   **Visual Subscription Pipeline:** A dedicated window interface to track membership queues and subscriptions seamlessly.

### Payments, Discounts & Promotions
*   **Sleek Checkout Flow:** Handles payment modes, total amounts, discount applications, actual paid amounts, and automated change calculation.
*   **Fixed Discounts:** Configurable automatically-applied discounts (e.g., 10% for Students, 20% for Seniors) restricted to allowed plans.
*   **Dynamic Promos:** Create and manage customized promo codes with specific start/end dates, active status, value type (fixed cash amount or percentage), and single-use validation per member.
*   **Refund Management:** Process refunds with granular tracking of refund reasons, custom internal notes, and processing staff metadata.

### Attendance & Automated Logging
*   **Quick Check-In / Check-Out:** Direct, frictionless tracking of member and trainer check-ins.
*   **Auto-Checkout Engine:** Automatic background checkout of any active sessions left open from previous days (defaulted to 10:00 PM) to maintain clean logging.

### Reports & Analytics Dashboard
*   **Financial Reports:** Keep track of revenue streams, transaction logs, and refund histories.
*   **Attendance Reports:** Gain insights on peak gym hours, check-in histories, and overall utilization.
*   **Discount & Promo Audits:** Analyze which marketing promos and fixed discounts are being redeemed the most.
*   **Membership Expirations:** View upcoming expirations and instantly trigger member notifications.

### Security, Customization & System Configuration
*   **Role-Based Security:** Secure user account system with Administrator and Staff levels.
*   **Password Security:** Secure hashing of credentials using `SHA-256`.
*   **Custom Branding & UI:** Configure gym name, address, contact details, email, and upload a custom brand logo.
*   **Premium Visual Customizer:** Change system theme (Dark/Light), Accent Color (e.g., Orange, Teal, Blue, etc.), and Button Styles (Gradient/Flat) on-the-fly.
*   **Backup & Restore:** Safety utilities to back up the system database or restore from a previous state in seconds.

---

## Tech Stack & Architecture

*   **UI Framework:** WPF (Windows Presentation Foundation) with XAML
*   **Language:** C# (.NET Core / .NET Framework)
*   **Database:** SQLite (`System.Data.SQLite`) with Write-Ahead Logging (WAL) enabled for concurrency protection.
*   **Data Layout:** Repository and helper-based architecture with clean XAML code-behinds, dynamic resource binding for theme configuration, and robust inputs validation.

---

## Repository Structure

```text
GymManagementSystem/
├── GymManagementSystem.slnx    # Visual Studio Solution Metadata
└── GymManagementSystem/        # Main WPF Application Project
    ├── App.xaml / App.xaml.cs  # Application startup, routing, and lifecycle hooks
    ├── AssemblyInfo.cs         # Assembly properties
    ├── Assets/                 # Image assets, logo templates, default graphics
    ├── Helpers/                # Core utilities
    │   ├── DatabaseHelper.cs   # SQLite database creation, schema migrations, and queries
    │   ├── InputValidator.cs   # Security checks, type safety, and form validation
    │   ├── NotificationHelper.cs # Expiring-member notifications engine
    │   └── ...
    ├── Models/                 # Strongly typed entity classes (Member, Instructor, PaymentRecord, etc.)
    ├── Styles/                 # WPF Resource Dictionaries for custom themes, colors, and controls
    └── Views/                  # UI Windows & Pages
        ├── MainViews/          # Primary views (Home dashboard, Members, Instructors, Payments, Attendance)
        ├── Reports/            # Analytics modules (Financials, Attendance, Refunds, Expirations)
        ├── Settings/           # Control panels (Appearance, Rates, Backups, Users, Branding Profile)
        └── Windows/            # Dialog boxes (Add/Edit member, trainer details, refund handling, login screen)
```

---

## Getting Started

### Prerequisites
*   Windows OS (since WPF is natively Windows-based)
*   [.NET SDK (version 6.0 or later)](https://dotnet.microsoft.com/download)
*   [Visual Studio 2022](https://visualstudio.microsoft.com/) (recommended with *.NET desktop development* workload) or [Rider](https://www.jetbrains.com/rider/)

### Installation & Run

1.  **Clone the Repository:**
    ```bash
    git clone https://github.com/yourusername/GymManagementSystem.git
    cd GymManagementSystem
    ```

2.  **Restore Dependencies:**
    Using the .NET CLI:
    ```bash
    dotnet restore
    ```

3.  **Build the Project:**
    ```bash
    dotnet build --configuration Release
    ```

4.  **Run the Application:**
    You can run it directly using the CLI:
    ```bash
    dotnet run --project GymManagementSystem/GymManagementSystem.csproj
    ```
    *Alternatively, open `GymManagementSystem.slnx` in Visual Studio and press **F5** to start.*

---

## Default Administrator Login

On the first application run, a local SQLite database file (`GymManagement.db`) will be automatically created and populated with default seed data. You can log in using the administrator credentials:

*   **Username:** `admin`
*   **Password:** `admin123`

> [!WARNING]
> *For security purposes, make sure to navigate to **Settings > User Accounts** and change the default password or create a personalized administrator account after your first login.*

---

## Database Schema

The database uses SQLite, which means zero complex server configurations! Here is a summary of the core tables:

*   **`Users`**: Holds administrative login credentials and authorization roles.
*   **`Members`**: Primary details of gym members, their membership expiry dates, assigned instructors, and discount types.
*   **`Instructors`**: Trainer attributes, specialization mappings, and contact information.
*   **`Payments`**: Ledger of financial transactions, payments received, discount details, and refunds issued.
*   **`Attendance` & `InstructorAttendance`**: Real-time sign-in/sign-out times mapped to dates.
*   **`Promos` & `Discounts`**: Configuration for automatic campaigns and coupon rules.
*   **`GymProfile`**: Branding preferences, theme selections, occupancy capacity, and contact headers.

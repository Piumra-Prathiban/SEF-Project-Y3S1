# Agent Context & System Blueprint: SEF Project Y3S1

> **Project Name:** SEF Project - Year 3 Semester 1 (SLIIT)  
> **Repository:** `SEF-Project-Y3S1`  
> **Domain:** Restaurant / Food Ordering & Enterprise Management Platform with Agentic AI Integration  
> **Last Updated:** September 2026

---

## 1. Executive Summary

This project is an enterprise-grade multi-tier restaurant and food ordering management platform developed for the **Software Engineering Frameworks (SEF)** module (Year 3 Semester 1, SLIIT). 

It combines:
1. **High-concurrency, resilient backend** (.NET 8 Web API + PostgreSQL + EF Core) with transactional guarantees for order management, payments, shipments, and inventory control.
2. **Interactive customer & staff web portal** (React 19 + Vite) with JWT authentication and feature-based modularity.
3. **Cross-platform mobile application** (Flutter / Dart) for mobile order tracking and customer interactions.
4. **Agentic AI Workflow Subsystem** designed to automate complex domain workflows (e.g., inventory anomaly detection, order triage, dynamic promotions, and automated support) with strict human-in-the-loop approvals and step validation.

---

## 2. Technology Stack

| Layer | Technologies & Libraries | Key Responsibilities |
|---|---|---|
| **Backend API** | **C# / .NET 8**, ASP.NET Core Web API | RESTful endpoints, auth enforcement, transactional business logic |
| **ORM & Database** | **Entity Framework Core 8**, **PostgreSQL** (`Npgsql.EntityFrameworkCore.PostgreSQL`) | Relational persistence, fluent schema configurations, database migrations, seed data |
| **Security & Auth** | **JWT Bearer**, ASP.NET Core Identity Core, BCrypt | Token creation/validation, role-based authorization (`Customer`, `Staff`, `Administrator`), password hashing |
| **Testing** | **xUnit**, **SQLite In-Memory**, EF Core In-Memory Test Fixtures | 100+ comprehensive automated tests (orders, payments, shipments, edge cases, auth) |
| **Frontend Web** | **React 19**, **Vite 8**, JavaScript (ESModules), modern CSS | Single Page App (SPA), auth context, state management, customer/admin views |
| **Mobile App** | **Flutter 3.x**, **Dart ^3.13.4**, Material 3 & Cupertino | Cross-platform client for iOS, Android, and Web |
| **CI / CD** | **GitHub Actions** (`.github/workflows/backend-ci.yml`) | Automated restore, release build, and xUnit test pipeline |

---

## 3. Repository & Directory Structure

```text
SEF Project-Y3S1/
└── SEF-Project-Y3S1/
    ├── .github/
    │   └── workflows/
    │       └── backend-ci.yml               # Automated CI for .NET 8 build & test
    ├── backend/
    │   ├── SEF_Project.Api/                 # Primary Web API Project
    │   │   ├── AI/                          # Agentic AI engine & tool handlers (WIP)
    │   │   ├── Authorization/               # Authorization policies & handlers
    │   │   ├── Configuration/               # Settings classes (JwtSettings, etc.)
    │   │   ├── Controllers/                 # API controllers (AuthController, OrdersController)
    │   │   ├── Data/                        # AppDbContext, SeedData, Fluent Configurations
    │   │   ├── DTOs/                        # Request and Response transfer objects
    │   │   ├── Middleware/                  # GlobalExceptionHandler (RFC 7807 ProblemDetails)
    │   │   ├── Migrations/                  # EF Core database migrations
    │   │   ├── Models/                      # Domain entities divided by functional domains
    │   │   │   ├── AgenticAI/               # Agent workflow, step, tool execution, and approval entities
    │   │   │   ├── Catalog/                 # Products, Categories, Variants, Inventory, Suppliers
    │   │   │   ├── Marketing/               # Campaigns, Promotions, Coupons, Redemptions
    │   │   │   ├── Orders/                  # Orders, Items, StatusHistory, Payments, Shipments
    │   │   │   ├── Shopping/                # Carts, CartItems
    │   │   │   ├── Address.cs, Customer.cs, Role.cs, User.cs
    │   │   │   └── Enums.cs                 # Domain enums across all modules
    │   │   ├── Repositories/                # Repository contracts and implementations
    │   │   ├── Services/                    # Domain business services (Auth, Orders)
    │   │   ├── Program.cs                   # App startup & DI composition
    │   │   └── appsettings.Development.json # Connection strings & JWT secrets
    │   └── SEF_Project.Api.Tests/           # xUnit integration & unit test suite (102 tests)
    ├── frontend/
    │   └── SEF-Project/                     # React 19 + Vite web client
    │       ├── src/
    │       │   ├── assets/                  # Images and static assets
    │       │   ├── components/              # Shared UI components
    │       │   ├── contexts/                # React contexts (AuthContext)
    │       │   ├── features/                # Feature-sliced modules (admin, auth, menu, orders, reservations)
    │       │   ├── hooks/                   # Custom reusable hooks
    │       │   ├── routes/                  # Navigation & route definitions
    │       │   ├── services/                # API communication layer (api.js, authService.js)
    │       │   └── utils/                   # Helper functions and formatters
    │       └── package.json
    ├── mobile/
    │   └── sef_project_mobile/
    │       └── sef_project/                 # Flutter mobile client
    │           ├── lib/                     # Flutter Dart source code (main.dart)
    │           └── pubspec.yaml             # Flutter dependencies
    ├── README.md                            # Database setup and local run instructions
    ├── SEF-Project.sln                      # Visual Studio / .NET Solution file
    └── agent.md                             # Agent guidance and architecture document (this file)
```

---

## 4. Domain & Database Architecture

The domain entities are organized cleanly into subdomains inheriting from either `BaseEntity` (`int Id`, `CreatedAt`, `UpdatedAt`) or `GuidEntity` (`Guid Id`, `CreatedAt`, `UpdatedAt`):

### 4.1. Identity & Access Management (IAM)
- **`User`**: Core user entity (`Email`, `PasswordHash`, `FirstName`, `LastName`, `RoleId`, `IsActive`).
- **`Role`**: Seeded roles: `1: Customer`, `2: Staff`, `3: Administrator`.
- **`Customer`**: 1-to-1 extension of `User` holding customer profile, loyalty data, and addresses.
- **`Address`**: Physical billing/delivery addresses associated with a Customer.

### 4.2. Catalog & Inventory Subdomain
- **`Category`**: Food/beverage categories (e.g., *Pizza*, *Pasta*, *Beverages*, *Desserts*).
- **`Product`**: Menu items with base details and descriptions.
- **`ProductVariant`**: Specific sellable SKUs (e.g., `PIZ-MARG-S` Small, `PIZ-MARG-L` Large) with size, specific price, and SKU code.
- **`ProductCategory`**: Many-to-many relationship linking products with categories.
- **`Supplier`**: Raw ingredient or item vendors.
- **`Inventory`**: Current on-hand quantity, reserved quantity, and safety stock thresholds.
- **`InventoryTransaction`**: Complete immutable ledger of stock changes (`Adjustment`, `Receipt`, `Sale`, `Reservation`, `ReservationRelease`, `Transfer`).

### 4.3. Shopping Cart Subdomain
- **`Cart`**: Customer active shopping cart with expiration time.
- **`CartItem`**: Items in the cart linked to specific `ProductVariant` and quantity.

### 4.4. Orders & Fulfillment Subdomain
- **`Order`**: Order header tracking `OrderNumber`, `CustomerId`, `Status`, `Subtotal`, `DiscountAmount`, `DeliveryFee`, `TotalAmount`, `SpecialInstructions`.
  - **Statuses:** `Pending`, `Confirmed`, `Preparing`, `Ready`, `Completed`, `Cancelled`, `Refunded`.
- **`OrderItem`**: Snapshot of variant SKU, item name, unit price, quantity, subtotal at checkout time.
- **`OrderAddress`**: Immutable snapshot of the delivery address at time of order creation.
- **`OrderStatusHistory`**: Audit trail recording old status, new status, changed by user, and reason.
- **`Payment`**: Payment record linked to order (`Card`, `Cash`, `OnlineTransfer`), transaction reference, amount, and status (`Pending`, `Completed`, `Failed`, `Refunded`).
- **`Shipment`**: Delivery tracking with carrier, tracking number, dispatched/delivered timestamps, and status (`Pending`, `Shipped`, `Delivered`, `Cancelled`).

### 4.5. Marketing & Promotion Subdomain
- **`Campaign`**: Marketing initiative container with start and end dates.
- **`Promotion`**: Discount rules (`PercentageDiscount`, `FixedAmountDiscount`, `BuyXGetY`, `FreeShipping`) with thresholds and limits.
- **`Coupon`**: Promotional promo codes with max redemptions and usage restrictions.
- **`CouponRedemption`**: Per-user coupon redemption history.

### 4.6. Agentic AI Subsystem Subdomain
Designed to model autonomous agent reasoning, multi-step execution, and human oversight:
- **`AgentWorkflow`**: Top-level workflow definition (`Objective`, `Status`, `PlanSummary`, `FinalOutcome`, `StartedAt`, `CompletedAt`).
  - **Statuses:** `Pending`, `Planning`, `InProgress`, `AwaitingApproval`, `Completed`, `Failed`, `Cancelled`.
- **`AgentWorkflowStep`**: Individual step within an agent plan (`StepNumber`, `Name`, `Description`, `Status`, `InputPayload`, `OutputPayload`).
  - **Statuses:** `Pending`, `Running`, `Completed`, `Failed`, `Skipped`.
- **`AgentToolExecution`**: Granular log of specific tools invoked by the agent (`ToolName`, `InputArguments`, `OutputResult`, `Status`, `ExecutionDurationMs`).
- **`AgentValidationResult`**: Automated rule verification output (`RuleName`, `Severity` [`Info`, `Warning`, `Error`], `Message`, `IsSatisfied`).
- **`AgentApproval`**: Human-in-the-loop checkpoint (`RequiredRole`, `Status` [`Pending`, `Approved`, `Rejected`], `ReviewedByUserId`, `DecisionNotes`).
- **`AgentWorkflowError`**: Detailed error telemetry capturing stack traces and recoverable error states.

---

## 5. API Endpoints Reference

Base URL: `http://localhost:5193/api` (Swagger: `http://localhost:5193/swagger`)

### Auth Endpoints (`/api/Auth`)
- `POST /api/auth/register` - Register a new customer account
- `POST /api/auth/login` - Authenticate with email/password; returns JWT bearer token
- `GET /api/auth/me` - Retrieve current authenticated user profile (`[Authorize]`)

### Order Management Endpoints (`/api/Orders`)
All order endpoints require `[Authorize]`. Customers only access their own orders; `Staff` and `Administrator` have full visibility.

| Method | Route | Access | Purpose |
|---|---|---|---|
| `POST` | `/api/orders` | Customer, Staff, Admin | Create order from cart / item list with address validation & stock reservation |
| `GET` | `/api/orders` | Authenticated | Query orders with pagination, status filter, and date ranges |
| `GET` | `/api/orders/{id}` | Authenticated | Retrieve single order details with items and address |
| `GET` | `/api/orders/{id}/status-history` | Authenticated | View audit trail of order status transitions |
| `PATCH`| `/api/orders/{id}/status` | Staff, Administrator | Transition order status (`Confirmed`, `Preparing`, etc.) |
| `POST` | `/api/orders/{id}/payments` | Authenticated | Submit payment for order |
| `GET` | `/api/orders/{id}/payments` | Authenticated | List all payment attempts for an order |
| `PATCH`| `/api/orders/{id}/payments/{paymentId}/status` | Staff, Administrator | Update payment status (e.g. mark Cash as completed) |
| `POST` | `/api/orders/{id}/shipments` | Staff, Administrator | Dispatch shipment and assign tracking details |
| `GET` | `/api/orders/{id}/shipments` | Authenticated | Track order shipments |
| `PATCH`| `/api/orders/{id}/shipments/{shipmentId}/status` | Staff, Administrator | Update shipment progress (`Shipped`, `Delivered`) |
| `POST` | `/api/orders/{id}/cancel` | Authenticated | Cancel order (with automatic refund triggering if paid) |

---

## 6. How to Run, Test & Develop

### 6.1. Backend (.NET 8)
1. Ensure **PostgreSQL** is running locally and database `sef_project_db` exists.
2. Verify `backend/SEF_Project.Api/appsettings.Development.json` has your PostgreSQL credentials.
3. Apply database migrations:
   ```bash
   dotnet ef database update --project backend/SEF_Project.Api
   ```
4. Run the API:
   ```bash
   dotnet run --project backend/SEF_Project.Api
   ```
5. Run the automated test suite:
   ```bash
   dotnet test SEF-Project.sln
   ```
   *(All 102 tests run using an in-memory SQLite provider and require no external DB dependency).*

### 6.2. Frontend (React + Vite)
1. Install dependencies:
   ```bash
   cd frontend/SEF-Project
   npm install
   ```
2. Start the development server:
   ```bash
   npm run dev
   ```
3. Run lint checks:
   ```bash
   npm run lint
   ```

### 6.3. Mobile (Flutter)
1. Get dependencies:
   ```bash
   cd mobile/sef_project_mobile/sef_project
   flutter pub get
   ```
2. Run on emulator or connected device:
   ```bash
   flutter run
   ```

---

## 7. Architectural Conventions & Guidelines for AI Agents

When implementing features, fixing bugs, or writing tests in this codebase, adhere to the following standards:

1. **Transactional Integrity for Orders & Stock**:
   - Order placement, cancellation, status changes, and payment updates must always be handled within an EF Core execution transaction (`IDbContextTransaction`) to prevent inventory desynchronization.
   - Always verify stock availability and deduct or release inventory reservations accurately using `InventoryTransactionType`.
2. **DTO & Separation of Concerns**:
   - Never expose raw EF Core entity models directly in API controller responses.
   - Always map through DTOs located in `SEF_Project.Api.DTOs.*`.
   - Maintain request validation at the DTO layer and domain business rule checks in the service layer (`Services/*`).
3. **Role-Based Authorization**:
   - Restrict operational actions (order status mutation, shipment generation, catalog updates) to `Staff` or `Administrator` roles using `[Authorize(Roles = "Staff,Administrator")]`.
   - Always enforce that customers can only query and cancel their own orders by verifying `GetCurrentUserId()`.
4. **Error Handling**:
   - Throw domain-specific exceptions or return RFC 7807 problem details.
   - All uncaught exceptions are intercepted by `GlobalExceptionHandler` returning consistent JSON.
5. **Agentic AI Expansion**:
   - When developing the `AI/` module or agent workers, map operations through the `AgentWorkflow`, `AgentWorkflowStep`, and `AgentApproval` tables.
   - Critical operations (such as automatic refund issuance, stock write-offs, or menu price adjustments) must trigger an `AgentApproval` entity in `Pending` state for human sign-off before execution.
6. **Testing Requirement**:
   - Any new backend service logic or controller endpoint must be accompanied by xUnit tests in `SEF_Project.Api.Tests` using the existing SQLite in-memory pattern. Keep all tests passing (`100% green`).

---

## 8. Current Project Status & Planned Milestones

- [x] Shared Domain Database Models (Users, Roles, Catalog, Inventory, Orders, Marketing, AgenticAI)
- [x] Database Fluent Configurations & Migration Setup
- [x] JWT Authentication & Password Hashing Service
- [x] Transactional Checkout & Inventory Reservation Flow
- [x] Order Status, Payment & Shipment Workflows
- [x] Order Cancellation & Automatic Refund Logic
- [x] Comprehensive xUnit Integration Tests (102 passing tests)
- [x] Frontend React Foundation (AuthContext, API client, Login/Me view)
- [ ] Frontend Feature Implementation (Menu browsing, Cart & Checkout UI, Order tracking, Staff dashboard)
- [ ] Mobile App Screen Implementation (Flutter UI integration with backend API)
- [ ] Catalog & Inventory Controller Endpoints (CRUD for products, categories, variants, and stock adjustments)
- [ ] Marketing & Promotion Service (Coupon validation and promotion calculation engine)
- [ ] Agentic AI Engine Implementation (`AI/` service executing autonomous workflows with human approval checkpoints)


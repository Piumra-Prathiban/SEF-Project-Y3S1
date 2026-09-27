# Clothic Demo Data Guide

The backend includes a development-only, idempotent demo data seeder. Its
source, identifiers and commands are tracked in Git, so every member can
create the same connected dataset in their own PostgreSQL database without
sharing a database dump or credentials.

## Commands

Run commands from `backend/SEF_Project.Api/`:

```bash
dotnet run -- seed-demo
dotnet run -- reset-demo
dotnet run -- reseed-demo
```

The commands automatically apply pending EF Core migrations before changing
demo data. They exit after completing the operation and do not start the API.

- `seed-demo` creates the dataset when it is absent. Running it again safely
  reports that the data already exists.
- `reset-demo` removes only the exact reserved demo records. Migration-owned
  catalogue/reference data and manually created records remain.
- `reseed-demo` runs reset followed by seed, which refreshes relative dates and
  restores the original demonstration state.

All three commands are blocked unless the ASP.NET Core environment is
`Development`.

## First-time setup

1. Configure PostgreSQL using
   `backend/SEF_Project.Api/appsettings.Development.json`.
2. Add a local password to the gitignored
   `backend/SEF_Project.Api/.env`:

   ```dotenv
   DemoData__Password=Demo123!
   ```

   Use any password with at least eight characters. The password is hashed
   using the application's existing ASP.NET Core Identity password service and
   is never stored as plain text in PostgreSQL.

3. Create the demo dataset:

   ```bash
   cd backend/SEF_Project.Api
   dotnet run -- seed-demo
   ```

4. Start the API normally:

   ```bash
   dotnet run
   ```

5. Start React or Flutter and sign in with one of the accounts below.

If the environment is not selected automatically by `launchSettings.json`, run
the command with `ASPNETCORE_ENVIRONMENT=Development` set in the shell.

## Demo accounts

All accounts use the value configured in `DemoData__Password`.

| Role | Email | Useful screens |
| --- | --- | --- |
| Customer | `customer@demo.clothic` | Storefront, wishlist, cart, checkout, orders, tracking, returns, profile and stylist |
| Staff | `staff@demo.clothic` | Products, inventory, purchase orders, fulfilment, returns and low-impact approvals |
| Administrator | `admin@demo.clothic` | All staff pages, marketing analytics, reports and high-impact approvals |

These accounts are separate from the Administrator optionally created through
`SeedAdmin__*` settings.

## Data created

The seeder reuses the migration-owned fashion catalogue so it does not create
duplicate categories, sizes, colours, products, variants or suppliers.

### Member 1 — Product and Inventory

- A received purchase order for the seeded footwear supplier.
- A matching inventory receipt history record.
- Existing seeded products and inventory remain the source of truth.

### Member 2 — Shopping and Customer Experience

- Customer profile and default Colombo address.
- Cart containing a hoodie variant.
- Wishlist containing the everyday jacket.
- Published five-star product review.

### Member 3 — Orders and Fulfilment

- Four orders covering `Ready`, `Completed` and `Cancelled` states.
- Gross, discount and net totals.
- Pending/completed/failed payment examples.
- Shipped, delivered and cancelled shipment examples.
- Status history and delivery-address snapshots.
- A completed order with a pending wrong-size return request.

### Member 4 — Marketing and Business Intelligence

- An active campaign whose dates are relative to the seeding date.
- An active 15% jacket promotion.
- Coupon code `DEMO15` and an audited redemption.
- Recent completed/ready/cancelled orders for dashboards, sales charts,
  promotion performance and demand insights.

### Agentic AI

- An Inventory & Promotion workflow awaiting human approval, including a
  structured proposal, server pricing, validation evidence and pending
  approval.
- A completed workflow with an approval and safe no-change outcome.

Dates are calculated from the time of seeding. Use `reseed-demo` when the data
becomes old or when you want the promotion and analytics windows refreshed.

## Safe removal

Remove the demonstration rows with:

```bash
dotnet run -- reset-demo
```

The reset uses exact reserved Guid values and these exact emails:

- `customer@demo.clothic`
- `staff@demo.clothic`
- `admin@demo.clothic`

Deletion runs inside a database transaction and follows dependency order:
agent records, coupon redemptions, returns, orders, reviews, inventory history,
purchase orders, marketing records, cart/wishlist data, address, customer and
users. A failure rolls the entire reset back.

Do not reuse the demo emails for real accounts. If manually created data starts
referencing a demo account, database foreign-key protection can make reset fail
instead of silently deleting that data.

## Completely removing the feature from the repository

First remove seeded rows from every developer database:

```bash
dotnet run -- reset-demo
```

Then remove:

1. `backend/SEF_Project.Api/Data/DemoDataSeeder.cs`
2. `backend/SEF_Project.Api.Tests/DemoDataSeederTests.cs`
3. The demo-command block in `backend/SEF_Project.Api/Program.cs`
4. The `DemoData__Password` example in `.env.example`
5. This guide and its README link

No rollback migration is required because the feature uses the existing schema
and does not add a demo-data table or columns.

## Full local database reset

Only when all local data may be destroyed:

```bash
dotnet ef database drop --force
dotnet ef database update
dotnet run -- seed-demo
```

This differs from `reset-demo`: it deletes manually entered data as well.

## Troubleshooting

### Password configuration error

If the command reports `DemoData__Password`, create or update `.env` and use a
password containing at least eight characters.

### Missing catalogue or roles

The database is behind the tracked migration chain. Run:

```bash
dotnet ef database update
dotnet run -- seed-demo
```

### Partial or reserved data collision

Run:

```bash
dotnet run -- reset-demo
dotnet run -- seed-demo
```

### Postgres.app rejects `trust` authentication

The application connection string already supports username/password
authentication. Configure Postgres.app's `pg_hba.conf` to use password
authentication for the local connection (for example `scram-sha-256`), reload
PostgreSQL, and confirm the configured user's password. This is a local server
configuration issue; the seed command cannot bypass it.

## Implementation and verification

The implementation is in
`backend/SEF_Project.Api/Data/DemoDataSeeder.cs`. Automated tests verify:

- connected data creation;
- password hashing;
- relative campaign dates;
- safe repeated seeding;
- demo-only reset;
- preservation of migration-owned products and roles; and
- rejection when the demo password is missing.


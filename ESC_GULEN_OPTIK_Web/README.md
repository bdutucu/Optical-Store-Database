# ESC Gülen Optik - Web Application

ASP.NET Core MVC web application for optical store management.

## Project Structure (Similar to Instructor's Pattern)

```
ESC_GULEN_OPTIK_Web/
├── Data/
│   └── DBConnection.cs          # Database helper class (like instructor's)
├── Models/
│   ├── Staff.cs
│   ├── Customer.cs
│   └── Product.cs
├── Controllers/
│   ├── HomeController.cs
│   ├── StaffController.cs
│   ├── CustomerController.cs
│   └── ProductController.cs
├── Views/
│   ├── Shared/_Layout.cshtml
│   ├── Home/
│   ├── Staff/
│   ├── Customer/
│   └── Product/
├── appsettings.json             # Connection string (like Web.config)
└── Program.cs
```

## Connection String

Located in `appsettings.json`:
```json
"ConnectionStrings": {
  "conStr": "Data Source=(LocalDB)\\MSSQLLocalDB; Database=ESC_GULEN_OPTIK; Integrated Security=True;"
}
```

## DBConnection Class Methods

Similar to instructor's `DBConnection.cs`:

| Method | Description | Example |
|--------|-------------|---------|
| `getSelect(sql)` | SELECT query, returns DataSet | `ds = dbcon.getSelect("SELECT * FROM Staff")` |
| `getSelectWithParams(sql, params)` | SELECT with parameters | `ds = dbcon.getSelectWithParams("SELECT * FROM Staff WHERE StaffID=@id", ("@id", 1))` |
| `execute(sql)` | INSERT/UPDATE/DELETE | `dbcon.execute("DELETE FROM Staff WHERE StaffID=1")` |
| `executeWithParams(sql, params)` | INSERT/UPDATE/DELETE with params | `dbcon.executeWithParams("UPDATE Staff SET Name=@n WHERE ID=@id", ("@n", "John"), ("@id", 1))` |
| `executeInsert(sql, params)` | INSERT, returns new ID | `int id = dbcon.executeInsert("INSERT...; SELECT SCOPE_IDENTITY();", ...)` |
| `executeStoredProcedure(name, params)` | Call stored procedure | `dbcon.executeStoredProcedure("proc_CreateSale", ("@CustomerID", 1))` |
| `getStoredProcedure(name, params)` | Call SP, returns DataSet | `ds = dbcon.getStoredProcedure("proc_SearchProducts", ("@Category", "FRAME"))` |

## Database Setup

1. Connect to LocalDB: `(LocalDB)\MSSQLLocalDB`
2. Create database: `CREATE DATABASE ESC_GULEN_OPTIK;`
3. Run `Database_Project_ButunTablolar.sql`
4. Run `Procedures_Taslak.sql`

## Run Application

```bash
cd ESC_GULEN_OPTIK_Web
dotnet run
```

Open: **http://localhost:5000** or **https://localhost:5001**

## Features

- ✅ Staff Management (CRUD)
- ✅ Customer Management (CRUD)
- ✅ Product Catalog with filtering
- ✅ Stored Procedure integration (`proc_SearchProducts`)
- ✅ Parameterized queries (SQL injection prevention)
- ✅ DataSet/DataAdapter pattern (like instructor)


# Postman Collection - BoilerPlate Authentication API

This Postman collection provides a complete workflow for testing tenant and user management functionality in the BoilerPlate Authentication API.

## Prerequisites

1. **API Running**: Ensure the BoilerPlate Authentication Web API is running (default: `http://localhost:8080`)
2. **Admin User**: The default admin user should be created with:
   - Username: `admin`
   - Password: `AdminPassword123!`
   - Role: Service Administrator
   - Tenant: System tenant (created automatically on first startup)

## Initial Setup

### Step 1: Find the System Tenant ID

Before running the collection, you need to set the `systemTenantId` collection variable. You can do this in one of the following ways:

**Option A: Query the Database**
```sql
SELECT "Id", "Name" FROM "Tenants" WHERE "Name" = 'System' OR "Name" = 'System Tenant';
```

**Option B: Use the API (if you have a token)**
1. Authenticate using any method to get a token
2. Call `GET /api/tenants` to find the System tenant
3. Copy the tenant ID

**Option C: Set Manually in Postman**
1. Open the collection variables
2. Set `systemTenantId` to the UUID of the System tenant

### Step 2: Import the Collection

1. Open Postman
2. Click **Import**
3. Select `BoilerPlate_Authentication_API.postman_collection.json`
4. The collection will be imported with default variables

### Step 3: Configure Collection Variables

Update these variables if needed:
- `baseUrl`: Default is `http://localhost:8080`
- `adminUsername`: Default is `admin`
- `adminPassword`: Default is `AdminPassword123!`
- `systemTenantId`: **MUST BE SET** - UUID of the System tenant

## Collection Structure

The collection is organized into the following folders:

### 1. Authentication
- **Get All Tenants (to find System Tenant)**: Attempts to get tenants (may fail without auth)
- **Get Service Admin Token**: Authenticates as Service Administrator and extracts tenant ID from JWT

### 2. Tenant Management
- **Create Tenant (Onboard)**: Creates a new tenant with default roles
- **Get Tenant by ID**: Retrieves tenant information
- **Get All Tenants**: Lists all tenants

### 3. User Management
- **NOTE: User Registration**: Important note about user creation
- **Get All Users in Tenant**: Lists all users in the current tenant
- **Get User by ID**: Retrieves user information
- **Assign Tenant Administrator Role**: Assigns Tenant Administrator role to a user
- **Get User Roles**: Lists roles assigned to a user
- **Get Tenant Admin Token**: Authenticates as Tenant Administrator
- **Get All Users (as Tenant Admin)**: Tests tenant-scoped access
- **Delete User**: Deletes a user within the tenant

### 4. Cross-Tenant Security Test
- **Attempt to Get Users from Different Tenant**: Verifies tenant isolation

### 5. Tenant Cleanup
- **Offboard Tenant**: Deletes all tenant data (users, roles, etc.)
- **Verify Tenant Deleted**: Confirms tenant deletion

## Workflow

The collection follows this testing workflow:

1. **Authenticate as Service Administrator**
   - Get OAuth token using admin credentials
   - Extract System tenant ID from JWT

2. **Create a Test Tenant**
   - Use Service Admin token to create a new tenant
   - Tenant is created with default roles (Tenant Administrator, User Administrator)

3. **Create Users** ⚠️
   - **IMPORTANT**: There is currently no public REST API endpoint for user registration
   - Users must be created manually through:
     - Direct database insertion
     - Programmatic call to `IAuthenticationService.RegisterAsync`
     - Or a future registration endpoint
   
   **For testing, create these users manually:**
   - **Tenant Admin User**:
     - TenantId: `{{testTenantId}}` (from collection variable)
     - UserName: `tenantadmin1`
     - Email: `tenantadmin1@test.com`
     - Password: `SecurePass123!`
     - FirstName: `Tenant`
     - LastName: `Admin`
   
   - **Regular User**:
     - TenantId: `{{testTenantId}}`
     - UserName: `testuser1`
     - Email: `testuser1@test.com`
     - Password: `SecurePass123!`
     - FirstName: `Test`
     - LastName: `User`

4. **Assign Tenant Administrator Role**
   - Use Service Admin token to assign Tenant Administrator role to the tenant admin user

5. **Authenticate as Tenant Administrator**
   - Get OAuth token using tenant admin credentials
   - This token will be scoped to the test tenant

6. **Test Tenant-Scoped Operations**
   - Get all users (should only see users in the tenant)
   - Verify tenant isolation

7. **Delete Test User**
   - Use Tenant Admin token to delete the regular user

8. **Cleanup**
   - Use Service Admin token to offboard the test tenant
   - Verify tenant and all associated data are deleted

## Running the Collection

### Option 1: Run Individual Requests
1. Set the `systemTenantId` variable
2. Run requests in order, following the workflow above

### Option 2: Use Collection Runner
1. Set the `systemTenantId` variable
2. Click **Run** on the collection
3. Select the requests you want to run
4. Click **Run BoilerPlate Authentication API**

**Note**: Some requests may fail if prerequisites aren't met (e.g., user creation). Review the test scripts to understand dependencies.

## Test Scripts

Each request includes test scripts that:
- Extract response data into collection variables
- Verify response status codes
- Validate response structure
- Check business logic (e.g., tenant isolation)

## Troubleshooting

### "401 Unauthorized" when getting Service Admin token
- Verify `systemTenantId` is set correctly
- Check that admin user exists with correct credentials
- Ensure the API is running

### "404 Not Found" when getting tenant
- Verify `testTenantId` is set (extracted from Create Tenant response)
- Check that the tenant was created successfully

### "User not found" errors
- Users must be created manually (see User Management section)
- Verify user exists in the database with correct tenant ID
- Check that user credentials match what's in the requests

### Cross-tenant access test doesn't work as expected
- The API automatically filters by tenant ID from JWT token
- Cross-tenant access is prevented at the API level
- The test verifies that users can only see their tenant's data

## Notes

- **User Registration**: Currently, there is no public REST API endpoint for user registration. Users must be created through database scripts or programmatically.
- **Tenant Isolation**: All API endpoints automatically filter by the tenant ID from the JWT token, ensuring tenant isolation.
- **Token Expiration**: JWT tokens expire after 15 minutes (configurable). Use refresh tokens to get new access tokens.
- **System Tenant**: The System tenant is created automatically on first startup and is used for Service Administrators.

## Future Enhancements

Consider adding:
- Public user registration endpoint (`POST /api/auth/register`)
- User creation endpoint for administrators (`POST /api/users`)
- Better error messages for missing prerequisites
- Automated user creation scripts for testing

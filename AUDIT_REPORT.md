# COMPREHENSIVE AUDIT REPORT
## Liasse Fiscale Application vs. Official Tunisian Tax Documentation

**Date**: 2026-09-01  
**Reference Documents**:
- Direction Générale des Impôts — Guide d'Utilisation
- Cahier des Charges Technique — Cas Général

---

## SECTION A: BUSINESS RULES AUDIT

### Key Findings Table

| Rule | Source | Current Implementation | Expected Behavior | Problem | Required Fix | Category |
|------|--------|----------------------|-------------------|---------|--------------|----------|
| **Authentication Mode** | Guide d'utilisation | Local email/password; users self-register | Integration with official télé-déclaration SSO or clearly labeled prototype | No mention of prototype mode; implies official system | Add authentication mode flag; clearly label as "LOCAL PROTOTYPE"; create IAuthenticationService abstraction | CRITICAL |
| **User vs Taxpayer** | Guide d'utilisation | Same entity; user IS taxpayer | User is authorized TO ACT FOR taxpayer; they are separate concepts | Missing authorization/mandate model | Create UserCompanyAuthorization model; support multiple companies per user | CRITICAL |
| **Taxpayer Identification** | Guide d'utilisation §2 | Automatic after login | Manual entry of matricule fiscal after login; verification of taxpayer details | Missing explicit identification workflow | Implement "Identify Taxpayer" screen after login | CRITICAL |
| **Matricule Fiscal Format** | Cahier des Charges | Basic regex validation (7 digits + letter) | 7 digits + 1 key + optional 5 chars; format validation separate from existence check | Partial; doesn't handle optional segment clearly | Enhance regex; document format precisely | HIGH |
| **Matricule Existence** | Guide d'utilisation | No check; accepts any formatted matricule | Must verify taxpayer exists in system before proceeding | Allows unknown taxpayers | Query official registry or database of valid taxpayers | HIGH |
| **Liasse Categories** | Guide d'utilisation | 5 categories defined; MicroCredits throws exception | Only categories matching taxpayer's activity should be selectable | User can select wrong category | Add validation: category must match taxpayer's CodeCategorie | HIGH |
| **ActeDeDepot (Acte)** | Cahier des Charges T_ActeDeDepot | Enum: Spontane, Rectification, Régularisation (correct) | Support distinct business workflows for each type | Implemented correctly | ✓ No change needed | OK |
| **NatureLiasse vs TypeDepot** | Cahier des Charges | NatureLiasse (Initiale/Rectificative/Cessation); TypeDepot (Definitif/Provisoire) | These are TWO separate dimensions; both required | Correctly separated | ✓ No change needed | OK |
| **Provisional after Definitive** | Cahier des Charges §11 | No enforcement | Business rule: if Definitive deposited, no new Provisional allowed for same context | Not enforced; no validation in code | Add backend rule in LiasseService: check existing Definitive before allowing new Provisional | CRITICAL |
| **Duplicate Prevention** | Cahier des Charges | Database index on (ContribuableId, Exercice) | Must consider ActeDeDepot, NatureLiasse, Status, and Provisional vs Definitive | Oversimplified; allows invalid duplicate combinations | Replace simple index with complex business logic (see Section D) | CRITICAL |
| **Deposit Completeness** | Guide d'utilisation §4 | No explicit check | Liasse must be complete (all required documents present and valid) before submission | Allows incomplete upload | Implement explicit "Verify Liasse" step | CRITICAL |
| **Verify Liasse** | Guide d'utilisation §5 | Not explicitly implemented as separate operation | Dedicated operation to verify category, fiscal year, required documents | Implicit in workflow; not user-facing | Create explicit `/api/liasses/{id}/verify` endpoint | HIGH |
| **F6004 Model Selection** | Cahier des Charges — Cas Général | Enum ModeleF6004 (Reference/Autorise); used in category selection | Correct; document whether all categories support both models | Implemented correctly | ✓ No change needed | OK |
| **Fiscal Year Dates** | Cahier des Charges | Separate fields: Exercice (int), DateDebutExercice, DateClotureExercice | Not hardcoded to 01/01 → 31/12; supports custom exercise periods | Correctly modeled | ✓ No change needed | OK |
| **Non-Valid Deposit State** | Guide d'utilisation §20 | StatutLiasse enum includes EnCoursDeSaisie, Deposee, Validee, Supprimee | After upload, deposit is "Non-valid" state; can be deleted by uploader | "Deposee" might represent this, unclear semantics | Clarify: rename EnCoursDeSaisie → "Deposee" or create explicit "NonValide" status | HIGH |
| **Delete Non-Valid Deposit** | Guide d'utilisation §22 | DocumentController.Delete exists; but permission checks unclear | Only non-valid deposits can be deleted; validated deposits are immutable | No status-based permission enforcement | Add authorization checks in DocumentController and LiasseController | CRITICAL |
| **Deposit History** | Guide d'utilisation §23 (Suivi des dépôts) | No TrackingController; no comprehensive history endpoint | Complete deposit history showing all fiscal years, acts, natures, statuses | Not implemented | Create comprehensive tracking/history endpoints | HIGH |
| **Download Historical Documents** | Guide d'utilisation §25 | DocumentController.Download exists but scoped to current liasse | Must download original XML/PDF from historical deposits; authorization required | Likely works but no protection against unauthorized access | Enforce authorization in DocumentController.Download | CRITICAL |
| **Accusé de Réception** | Guide d'utilisation §26 | ReceiptService/ReceiptController exist | Only generate for Validated deposits; associate with correct deposit | Potentially allows generation for non-validated | Add status check before generating receipt | HIGH |
| **Dashboard Actions** | Guide d'utilisation §27 | Frontend likely shows all actions; backend doesn't restrict | Actions depend on deposit state (e.g., "Nouveau dépôt" only if no current) | No state-based action filtering | Implement conditional action availability based on deposit status | HIGH |
| **Authorization: Company-Level** | Guide d'utilisation | User can access all their companies' deposits | User must be explicitly authorized for each company before access | User might have access to wrong company | Add UserCompanyAuthorization table; check on every request | CRITICAL |
| **Audit Logging** | Implicit (tax requirement) | No audit log implementation | Log all actions: authentication, search, deposit creation, upload, validation, deletion | Missing entirely | Create AuditLog table and log all operations | CRITICAL |
| **Provisional vs Definitive Transition** | Cahier des Charges §11 | No validation | Provisional → Definitive OK; Definitive → new Provisional NOT OK | Not enforced | Add backend validation rule | CRITICAL |
| **XML Header Coherence** | Cahier des Charges §18 | Validation exists in DocumentController but incomplete | MatriculeFiscalDeclarant = Contribuable.MatriculeCourt; Exercice = Liasse.Exercice; ActeDeDepot/NatureDepot must match | Partial validation; missing some checks | Enhance XML header validation in ValidationService | HIGH |

---

## SECTION B: AUTHENTICATION AUDIT

### Current Implementation
```
Register (email + password + optional matricule)
     ↓
User created; optionally linked to Contribuable
     ↓
Login (email + password)
     ↓
JWT token issued
     ↓
Access to `/api/auth/me`
     ↓
No further taxpayer identification
```

### Issues

1. **No Official Integration Path**
   - Current: Self-contained local authentication
   - Expected: Architecture-ready for official SSO
   - **Fix Required**: Create abstraction:
     ```csharp
     interface IAuthenticationService
     {
         Task<AuthenticationResult> AuthenticateAsync(string credential, string password);
         Task<User> GetAuthenticatedUserAsync(string token);
     }
     
     class LocalAuthenticationService : IAuthenticationService { }
     class OfficialAuthenticationService : IAuthenticationService { }
     ```

2. **Missing Taxpayer Identification Workflow**
   - After login, user should enter matricule fiscal
   - System searches and verifies taxpayer exists
   - Display taxpayer details before proceeding
   - **Current Gap**: Automatic; no explicit verification step
   - **Fix Required**: Create `POST /api/auth/identify-taxpayer` endpoint

3. **No Prototype Label**
   - **Issue**: Application implies it's an official system
   - **Fix Required**: 
     - Environment flag: `IsPrototypeMode=true`
     - Display banner: "Prototype - Local Authentication"
     - Document clearly in README

4. **Password Security**
   - ✓ BCrypt hashing implemented correctly
   - ✓ JWT expiration configured
   - **Issues**: No password policy enforcement; no rate limiting
   - **Fix Required**: Add password validation rules; implement login rate limiting

5. **No Professional Authorization**
   - Cannot represent "tax professional acting on behalf of taxpayer"
   - **Fix Required**: Create Mandate model:
     ```csharp
     class UserMandate
     {
         int Id;
         int UserId;           // Professional
         int ContribuableId;   // Client
         DateTime StartDate;
         DateTime? EndDate;
         string Permissions;   // Comma-separated: "deposit,view,download"
     }
     ```

### Required Authentication Architecture

```
Frontend
    ↓
[Login Form]
    ↓
POST /api/auth/login → AuthenticationService.AuthenticateAsync()
    ↓
    ├─→ LocalAuthenticationService (Prototype)
    │       ├─ Email/password lookup
    │       ├─ BCrypt verify
    │       └─ Generate JWT
    │
    └─→ OfficialAuthenticationService (Future)
            ├─ Call official API
            ├─ Validate certificate
            └─ Generate JWT
    ↓
JWT Token
    ↓
[Identify Taxpayer]
    ↓
POST /api/auth/identify-taxpayer
    ↓
Verify Matricule Fiscal
Verify User Authorization
    ↓
Access Liasse Service
```

---

## SECTION C: DEPOSIT LIFECYCLE STATE MACHINE

### Corrected State Flow

```
┌─────────────────────────────────────────────────────────────┐
│                     [CREATE DEPOSIT]                        │
│   - Select category, fiscal year, acte, nature              │
│   - Specify dates                                            │
│   - System determines required documents                     │
└────────────────────────┬────────────────────────────────────┘
                         ↓
                  [NON-VALID STATE]
                  (Newly created)
                         ↓
        ┌────────────────┴─────────────────┐
        ↓                                  ↓
    [UPLOAD]                         [DELETE]
    Upload documents                 (Only from non-valid)
         ↓                           (Removes all files)
    File stored                           ↓
    Document status:              [DELETED]
    EnAttenteDeValidation         (Final state)
         ↓
    ┌────────────────┐
    ↓                ↓
[VALIDATE]      [INVALID]
Validate each    (One or more
document         documents fail
against XSD      validation)
         ↓                ↓
     [VALID]       [ERROR STATE]
                   Cannot proceed
                   User may:
                   - Re-upload document
                   - Delete & start over
                        ↓
                   [Back to UPLOAD]
         ↓
  [SUBMIT DEPOSIT]
  Confirm submission
  Verify completeness
         ↓
  [VALIDATED]
  Deposit accepted
  by system
         ↓
  ┌──────────────────────────┐
  ↓                          ↓
[TRACKING]            [GENERATE RECEIPT]
View history          Create accusé de réception
Download original     (Only from Validated)
  files                      ↓
  (Immutable)          [RECEIPT GENERATED]
  Cannot edit
  Cannot delete
```

### Status Values (Unified)

```csharp
public enum StatutLiasse
{
    // Creation phase
    Brouillon,              // Draft (new, not yet populated)
    EnSaisie,               // In entry (documents being uploaded)
    
    // Validation phase  
    EnAttenteDeValidation,  // Awaiting validation (all docs uploaded)
    EnErreur,               // Validation errors (one or more documents invalid)
    
    // Accepted phase
    Deposee,                // Deposited (locally accepted, waiting validation)
    Validee,                // Validated (officially accepted)
    
    // Terminal states
    Supprimee,              // Deleted (only from Brouillon/EnSaisie/EnErreur)
    Rejetee                 // Rejected (by validation engine)
}
```

### Key Rules

1. **Only from Non-Valid states** (`Brouillon`, `EnSaisie`, `EnErreur`):
   - Can upload documents
   - Can delete entire deposit
   - Can abandon

2. **Provisional vs Definitive Rule**:
   ```
   If exists: Liasse where 
       ContribuableId = current.ContribuableId 
       AND Exercice = current.Exercice
       AND ActeDeDepot = current.ActeDeDepot
       AND TypeDepot = Definitif
       AND Statut = Validee
   
   Then: REJECT new Liasse with TypeDepot = Provisoire
   ```

3. **Rectification/Regularisation**:
   - Can exist alongside Spontane for same fiscal year
   - Each is a distinct business event
   - Ordering: Spontane → Rectification → Regularisation (not enforced but important)

4. **Deletion Permission**:
   ```csharp
   bool CanDelete = Statut is Brouillon or EnSaisie or EnErreur
   ```

---

## SECTION D: DUPLICATE PREVENTION LOGIC

### Current Implementation
- Database index: `(ContribuableId, Exercice)`
- **Problem**: Doesn't prevent multiple valid deposits for same fiscal year

### Correct Business Model

**Key Insight**: A unique deposit is defined by:
- Contribuable (taxpayer)
- Fiscal Exercise (year)  
- Acte de Dépôt (Spontane/Rectification/Régularisation)
- Nature du Dépôt (Provisoire/Définitif)

**Rule**: For a given (Contribuable, FiscalYear, ActeDeDepot), allow:
- **ONE** Definitive + **Multiple** Provisional versions

**Invalid Combinations**:
```
Case 1: Definitive already exists
        ├─ NEW Provisional for SAME (Acte, Year) → REJECT

Case 2: Provisional exists
        ├─ NEW Definitive for SAME (Acte, Year) → ALLOW (provisional replaced)

Case 3: Spontane Definitive exists
        ├─ NEW Rectification Spontane → ALLOW (different ActeDeDepot)
        ├─ NEW Rectification Definitive → ALLOW (different ActeDeDepot)
        ├─ NEW Regularisation → ALLOW (different ActeDeDepot)

Case 4: Multiple Provisional versions
        ├─ NEW Provisional for SAME (Acte, Year) → REJECT
```

### Implementation Strategy

**Step 1: Database Constraint**
```csharp
// In OnModelCreating:
modelBuilder.Entity<Liasse>()
    .HasIndex(l => new { l.ContribuableId, l.Exercice, l.ActeDeDepot })
    .HasName("idx_liasse_contribuable_exercice_acte");
    
// NOT unique yet; logic added in code
```

**Step 2: Business Logic in LiasseService.CreerAsync**

```csharp
public async Task<Liasse> CreerAsync(CreerLiasseDto dto)
{
    // Validation 1: Provisional after Definitive rule
    var existingDefinitive = await _db.Liasses
        .Where(l => l.ContribuableId == contribuableId
                && l.Exercice == dto.Exercice
                && l.ActeDeDepot == dto.ActeDeDepot
                && l.TypeDepot == TypeDepot.Definitif
                && l.Statut != StatutLiasse.Supprimee)
        .FirstOrDefaultAsync();
    
    if (existingDefinitive != null && dto.TypeDepot == TypeDepot.Provisoire)
    {
        throw new InvalidOperationException(
            "Une Liasse définitive existe déjà pour cet exercice et acte. " +
            "Un dépôt provisoire ne peut pas être transféré après une Liasse définitive.");
    }

    // Validation 2: Multiple provisionals check
    var existingProvisional = await _db.Liasses
        .Where(l => l.ContribuableId == contribuableId
                && l.Exercice == dto.Exercice
                && l.ActeDeDepot == dto.ActeDeDepot
                && l.TypeDepot == TypeDepot.Provisoire
                && l.Statut != StatutLiasse.Supprimee
                && l.Statut != StatutLiasse.Rejetee)
        .FirstOrDefaultAsync();
    
    if (existingProvisional != null && dto.TypeDepot == TypeDepot.Provisoire)
    {
        throw new InvalidOperationException(
            "Une Liasse provisoire existe déjà pour cet exercice et acte. " +
            "Veuillez finaliser la première avant d'en créer une nouvelle.");
    }

    // Validation 3: Rectification requires previous Spontane (business rule)
    if (dto.ActeDeDepot == ActeDeDepot.Rectification)
    {
        var hasSpontane = await _db.Liasses
            .Where(l => l.ContribuableId == contribuableId
                    && l.Exercice == dto.Exercice
                    && l.ActeDeDepot == ActeDeDepot.Spontane
                    && l.Statut != StatutLiasse.Supprimee)
            .AnyAsync();
        
        if (!hasSpontane)
        {
            throw new InvalidOperationException(
                "Une Rectification nécessite une Liasse Spontanée préalable.");
        }
    }

    // Create new Liasse
    var liasse = new Liasse { /* ... */ };
    _db.Liasses.Add(liasse);
    await _db.SaveChangesAsync();
    return liasse;
}
```

**Step 3: Frontend Enforcement**
- Disable "Create Deposit" if invalid state detected
- Show reason why action unavailable
- But backend is the final authority

---

## SECTION E: DATABASE CHANGES REQUIRED

### New Entities

#### 1. AuditLog (NEW)
```csharp
public class AuditLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public DateTime Timestamp { get; set; }
    
    public string Action { get; set; }              // "Login", "CreateDeposit", "UploadDocument", "DeleteDeposit"
    public string? EntityType { get; set; }         // "Liasse", "Document", "User"
    public int? EntityId { get; set; }
    
    public int? ContribuableId { get; set; }        // For company-level actions
    
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    
    public string? OldValue { get; set; }           // JSON for complex changes
    public string? NewValue { get; set; }           // JSON for complex changes
    
    public string? Notes { get; set; }              // Additional context
}
```

#### 2. UserCompanyAuthorization (RENAME/REFACTOR)
```csharp
// Currently implied in User.Contribuables relationship
// Make explicit:

public class UserCompanyAuthorization
{
    public int Id { get; set; }
    
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public int ContribuableId { get; set; }
    public Contribuable Contribuable { get; set; } = null!;
    
    public AuthorizationType Type { get; set; }     // "Direct" or "Professional"
    public string? MandateReference { get; set; }   // For professionals
    
    public DateTime DateAuthorized { get; set; }
    public DateTime? DateExpired { get; set; }      // For time-limited mandates
    
    public string Permissions { get; set; }         // "all" or comma-separated
    
    public bool IsActive { get; set; } = true;
}

public enum AuthorizationType
{
    Direct,                 // User = Taxpayer
    Professional,           // User = Tax professional acting for taxpayer
    Representative          // User = Company representative
}
```

#### 3. AuthenticationMode (Configuration)
Not a database table; configuration in appsettings:
```json
{
  "Authentication": {
    "Mode": "Local",  // "Local" or "Official"
    "LocalPrototype": {
      "Enabled": true,
      "DisplayBanner": true
    },
    "Official": {
      "ApiUrl": "https://...",
      "CertificatePath": "..."
    }
  }
}
```

### Modified Entities

#### User.cs Changes
```csharp
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    
    // Replace:
    // public List<Contribuable> Contribuables { get; set; }
    
    // With:
    public List<UserCompanyAuthorization> Authorizations { get; set; } = new();
    
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    
    // Add audit fields
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
}
```

#### Liasse.cs Changes
```csharp
public class Liasse
{
    // ... existing fields ...
    
    // Add:
    public int SubmittedBy { get; set; }            // UserId who created
    public DateTime? DateSubmission { get; set; }   // When submitted
    
    public int? ReviewedBy { get; set; }            // UserId who validated
    public DateTime? DateReview { get; set; }
    public string? ReviewNotes { get; set; }        // Validation notes
}
```

#### DocumentFiscal.cs Changes
```csharp
public class DocumentFiscal
{
    // ... existing fields ...
    
    // Add for audit:
    public int? UploadedBy { get; set; }             // UserId
    public DateTime? DateUpload { get; set; }        // Already exists, keep
    public string? ChecksumSha256 { get; set; }      // For integrity
}
```

### Migration Strategy

```csharp
// Migration: AddAuditLogAndAuthorization
public partial class AddAuditLogAndAuthorization : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Create UserCompanyAuthorization table
        migrationBuilder.CreateTable(
            name: "UserCompanyAuthorizations",
            columns: table => new
            {
                Id = table.Column<int>(),
                UserId = table.Column<int>(),
                ContribuableId = table.Column<int>(),
                Type = table.Column<string>(),
                // ... other columns ...
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserCompanyAuthorizations", x => x.Id);
                table.ForeignKey("FK_UserCompanyAuthorizations_Users_UserId",
                    x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_UserCompanyAuthorizations_Contribuables_ContribuableId",
                    x => x.ContribuableId, "Contribuables", "Id", onDelete: ReferentialAction.Cascade);
            });

        // 2. Migrate data from User.Contribuables → UserCompanyAuthorizations
        migrationBuilder.Sql(@"
            INSERT INTO ""UserCompanyAuthorizations"" (""UserId"", ""ContribuableId"", ""Type"", ""DateAuthorized"", ""IsActive"", ""Permissions"")
            SELECT uc.""UserId"", uc.""ContribuableId"", 'Direct', NOW(), true, 'all'
            FROM ""UserContribuables"" uc
        ");

        // 3. Drop old table
        migrationBuilder.DropTable("UserContribuables");

        // 4. Create AuditLog table
        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                Id = table.Column<int>(),
                UserId = table.Column<int>(nullable: true),
                Timestamp = table.Column<DateTime>(),
                // ... other columns ...
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.Id);
                table.ForeignKey("FK_AuditLogs_Users_UserId",
                    x => x.UserId, "Users", "Id", onDelete: ReferentialAction.SetNull);
            });

        // 5. Add columns to existing tables
        migrationBuilder.AddColumn<int>("SubmittedBy", "Liasses", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<DateTime?>("DateSubmission", "Liasses", nullable: true);
        // ... etc ...
    }
}
```

---

## SECTION F: API CHANGES REQUIRED

### New Endpoints

#### 1. Authentication & Identification

```
POST /api/auth/login
    Input: { email, password }
    Output: { token }
    Behavior: Issue JWT (mode-dependent)
    
POST /api/auth/identify-taxpayer
    [Authorized]
    Input: { matriculeFiscal }
    Output: { 
        id, 
        matriculeFiscal, 
        nomOuRaisonSociale, 
        adresse, 
        activite, 
        categorie,
        authorized: bool
    }
    Behavior: Verify taxpayer exists; verify user authorized; return details
    
GET /api/auth/me
    [Authorized]
    Output: { user: {...}, companies: [...] }
    
GET /api/auth/companies
    [Authorized]
    Output: List of companies user is authorized for
    
POST /api/auth/select-company
    [Authorized]
    Input: { contribuableId }
    Behavior: Set session context to this company
    Output: { selected: true, company: {...} }
```

#### 2. Liasse Management (Enhanced)

```
POST /api/liasses
    [Authorized]
    Input: {
        contribuableId,
        exercice,
        dateDebut,
        dateCloture,
        categorie,
        acteDeDepot,
        natureDuDepot,
        modeleF6004
    }
    Validations:
        - User authorized for contribuableId
        - Contribuable exists
        - Category matches taxpayer activity
        - Provisional after Definitive rule
        - Rectification requires prior Spontane
    Output: { id, statut, documents: [...] }
    
GET /api/liasses/{id}
    [Authorized]
    Output: Full liasse with documents and status
    Authorization: User must be authorized for liasse.contribuableId
    
GET /api/liasses/verify/{id}
    [Authorized]
    Description: Verify liasse is complete/valid
    Output: {
        isComplete: bool,
        requirementsStatus: {
            required: { total, uploaded, valid },
            optional: { total, uploaded }
        },
        canSubmit: bool,
        issues: [...]
    }
    
DELETE /api/liasses/{id}
    [Authorized]
    Condition: Only if Statut is Brouillon/EnSaisie/EnErreur
    Output: { deleted: true }
    
POST /api/liasses/{id}/submit
    [Authorized]
    Behavior: Mark as submitted (Deposee); verify completeness
    Output: { statut: "Deposee" }
```

#### 3. Document Upload (Enhanced)

```
POST /api/liasses/{liasseId}/documents/{codeDocument}/upload
    [Authorized]
    Input: multipart/form-data { file }
    Validations:
        - User authorized for liasse
        - Document format correct
        - XML/PDF parsing
        - XSD validation
        - Filename format
        - Header coherence
        - Taxpayer match
    Output: { 
        codeDocument, 
        statut: "EnAttenteDeValidation",
        errors: [...]
    }
    
DELETE /api/liasses/{liasseId}/documents/{codeDocument}
    [Authorized]
    Condition: Only if liasse status allows
    Output: { deleted: true }
```

#### 4. Validation

```
POST /api/validation/document
    Input: multipart/form-data { file, codeDocument }
    Output: {
        isValid: bool,
        errors: [{ line, element, message, severity, source }]
    }
    Description: Dry-run validation without storage
```

#### 5. Tracking & History

```
GET /api/liasses/tracking/company/{contribuableId}
    [Authorized]
    Output: [
        {
            id, 
            exercice,
            acteDeDepot,
            natureDuDepot,
            statut,
            dateDepot,
            reference
        }
    ]
    Authorization: User must be authorized for contribuableId
    
GET /api/liasses/tracking/history
    [Authorized]
    Query: { year, acte, nature, status }
    Output: Full history with filters
```

#### 6. Receipts

```
GET /api/receipts/{depositId}
    [Authorized]
    Output: Receipt details or error if not generated
    
POST /api/receipts/{depositId}/generate
    [Authorized]
    Condition: Only if liasse.statut == Validee
    Output: { receiptNumber, generatedAt, filePath }
    
GET /api/receipts/{depositId}/download
    [Authorized]
    Output: PDF file
```

#### 7. Audit Logging (Internal)

```
GET /api/admin/audit-logs
    [Admin only]
    Query: { userId, action, entityType, fromDate, toDate }
    Output: [{ id, userId, timestamp, action, entityType, entityId, notes }]
```

### Modified Endpoints

#### DocumentController Modifications

```csharp
[HttpPost("{codeDocument}")]
public async Task<IActionResult> Uploader(int liasseId, string codeDocument, IFormFile? file, IFormFile? fichier)
{
    // ADD: Authorization check
    var liasse = await _db.Liasses
        .Include(l => l.Contribuable)
        .Include(l => l.Documents)
        .FirstOrDefaultAsync(l => l.Id == liasseId);

    if (liasse is null)
        return NotFound();

    // NEW: Verify user is authorized for this contribuable
    var userId = /* extract from JWT */;
    var isAuthorized = await _db.UserCompanyAuthorizations
        .Where(a => a.UserId == userId 
                && a.ContribuableId == liasse.ContribuableId 
                && a.IsActive)
        .AnyAsync();
    
    if (!isAuthorized)
        return Forbid("You are not authorized to access this taxpayer.");

    // NEW: Check liasse status allows upload
    if (!new[] { StatutLiasse.Brouillon, StatutLiasse.EnSaisie, StatutLiasse.EnErreur }
        .Contains(liasse.Statut))
        return BadRequest("This liasse cannot accept new documents in its current status.");

    // ... rest of validation ...
    
    // NEW: Log the action
    await _auditService.LogAsync(new AuditLog
    {
        UserId = userId,
        Action = "UploadDocument",
        EntityType = "DocumentFiscal",
        EntityId = documentSlot.Id,
        ContribuableId = liasse.ContribuableId,
        Timestamp = DateTime.UtcNow
    });

    // ... existing upload logic ...
}

[HttpDelete("{codeDocument}")]
public async Task<IActionResult> Detacher(int liasseId, string codeDocument)
{
    // ADD: Check status before deletion
    var liasse = await _db.Liasses.FirstOrDefaultAsync(l => l.Id == liasseId);
    if (liasse is null)
        return NotFound();

    if (!new[] { StatutLiasse.Brouillon, StatutLiasse.EnSaisie, StatutLiasse.EnErreur }
        .Contains(liasse.Statut))
        return BadRequest("This document cannot be deleted once the liasse is submitted.");

    // ... existing delete logic ...
}
```

---

## SECTION G: FRONTEND CHANGES REQUIRED

### New Pages/Components

#### 1. Pre-Dashboard: Taxpayer Identification
```
After login (if mode = Local):
┌─────────────────────────────────────┐
│     IDENTIFY YOURSELF (PROTOTYPE)   │
├─────────────────────────────────────┤
│  Matricule Fiscal:                  │
│  [________-____-__-____]            │
│                                     │
│  [ Search ] [ Cancel ]              │
└─────────────────────────────────────┘

If not found:
┌─────────────────────────────────────┐
│  ❌ Taxpayer not found in database  │
│                                     │
│  Contact system administrator       │
│  or create account with registration│
│                                     │
│  [ Back ] [ Register New ]          │
└─────────────────────────────────────┘

If found:
┌─────────────────────────────────────┐
│  Taxpayer Information               │
├─────────────────────────────────────┤
│  Name: ACME SA                      │
│  Matricule: 1234567A                │
│  Activity: Commerce de gros         │
│  Category: Cas Général              │
│  Address: 15 Av Habib Bourguiba     │
│                                     │
│  [ Confirm ] [ Change ]             │
└─────────────────────────────────────┘
```

#### 2. Dashboard Redesign
```
Show company context + available actions based on state

┌─────────────────────────────────────┐
│  ACME SA (1234567A)  [Switch ▼]     │
├─────────────────────────────────────┤
│  FISCAL YEAR: 2026                  │
│  Status: No current deposit         │
│                                     │
│  Available Actions:                 │
│  [ ➕ Start New Deposit ]            │
│  [ 📋 View History ]                │
│                                     │
│  Recent Deposits:                   │
│  2025 | Spontané | Définitif        │
│  2024 | Spontané | Définitif        │
│  2023 | Spontané | Définitif        │
└─────────────────────────────────────┘
```

#### 3. Create Deposit Form (Enhanced)
```
Step 1: Select Parameters
┌─────────────────────────────────────┐
│  CREATE NEW DEPOSIT                 │
├─────────────────────────────────────┤
│  Fiscal Year: [2026▼]               │
│  Start Date: [01/01/2026]           │
│  End Date: [31/12/2026]             │
│                                     │
│  Category: [Cas Général▼]           │
│            (Read-only; based on     │
│             taxpayer activity)      │
│                                     │
│  Nature: ⊙ Initiale                 │
│         ◯ Rectificative             │
│         ◯ Cessation d'activité      │
│                                     │
│  Acte: ⊙ Spontané                   │
│       ◯ Rectification               │
│       ◯ Régularisation              │
│                                     │
│  Type: ⊙ Définitif                  │
│       ◯ Provisoire                  │
│                                     │
│  (Notes on restrictions shown here) │
│                                     │
│  [ Next ] [ Cancel ]                │
└─────────────────────────────────────┘

Step 2: Verify Liasse
┌─────────────────────────────────────┐
│  VERIFY LIASSE                      │
├─────────────────────────────────────┤
│  Category: Cas Général              │
│  Fiscal Year: 2026                  │
│  Nature: Initiale                   │
│  Acte: Spontané                     │
│                                     │
│  REQUIRED DOCUMENTS:                │
│  □ F6001 — Bilan actif              │
│  □ F6002 — Bilan passif             │
│  □ F6003 — État de résultat         │
│  □ F6004 — État de flux (Réf)       │
│  □ F6005 — Détermination résult fiscal │
│  □ F6006 — Notes & principes        │
│  □ F6007 — Faits marquants          │
│                                     │
│  OPTIONAL DOCUMENTS:                │
│  □ F6019 — Annexes & notes (PDF)   │
│                                     │
│  [ Create Deposit ] [ Cancel ]      │
└─────────────────────────────────────┘
```

#### 4. Upload & Validation Page
```
┌─────────────────────────────────────┐
│  UPLOAD DOCUMENTS                   │
│  Status: En cours de saisie         │
├─────────────────────────────────────┤
│  Fiscal Year: 2026                  │
│  Category: Cas Général              │
│                                     │
│  REQUIRED (7/7):                    │
│  ┌─────────────────────────────────┐│
│  │ F6001 — Bilan actif             ││
│  │ Status: ✓ Validée               ││
│  │ File: F6001-1234567A-2026.xml   ││
│  │ [👁 View] [🔄 Replace] [❌ Delete]││
│  └─────────────────────────────────┘│
│  ┌─────────────────────────────────┐│
│  │ F6002 — Bilan passif            ││
│  │ Status: ❌ Erreurs détectées     ││
│  │ [Click for details]             ││
│  │ [📤 Upload] [❌ Clear]           ││
│  └─────────────────────────────────┘│
│  ...                                │
│                                     │
│  OPTIONAL (0/1):                    │
│  ...                                │
│                                     │
│  Completion: ████░░░░░ 57%          │
│                                     │
│  [ Validate ] [ Save Draft ]        │
│  [ Complete & Submit ]              │
│  [ Delete Deposit ]                 │
└─────────────────────────────────────┘
```

#### 5. Validation Results Modal
```
┌─────────────────────────────────────┐
│  VALIDATION ERRORS — F6002          │
├─────────────────────────────────────┤
│                                     │
│  File: F6002-1234567A-2026.xml      │
│  Date: 2026-09-01 14:32             │
│                                     │
│  🔴 CRITICAL ERRORS (2)             │
│                                     │
│  Line 45: Missing required element  │
│  <Rubrique>11200</Rubrique>         │
│  Message: Element is mandatory      │
│           in XSD schema              │
│  Source: XSD Validation              │
│                                     │
│  Line 128: Invalid value            │
│  <Montant>ABC</Montant>             │
│  Message: Expected decimal,         │
│           got string                │
│  Source: Schema validation           │
│                                     │
│  [ ← Back to Upload ]               │
│  [ 📥 Re-upload ]                   │
│  [ 💾 Save Details ]                │
└─────────────────────────────────────┘
```

#### 6. History/Tracking Page
```
┌─────────────────────────────────────┐
│  SUIVI DES DÉPÔTS                   │
│  ACME SA (1234567A)                 │
├─────────────────────────────────────┤
│  Filters:                           │
│  [Year▼] [Acte▼] [Nature▼] [Status▼]│
│  [Filter] [Reset]                   │
│                                     │
│  ┌─────────────────────────────────┐│
│  │ 2025 | Spontané | Définitif     ││
│  │ Status: ✓ Validée               ││
│  │ Date: 2026-01-15                ││
│  │ Ref: TN/2025/0001234            ││
│  │                                 ││
│  │ [👁 Details] [📥 Download]      ││
│  │ [📄 Receipt] [HTML View]        ││
│  └─────────────────────────────────┘│
│  ┌─────────────────────────────────┐│
│  │ 2024 | Spontané | Définitif     ││
│  │ Status: ✓ Validée               ││
│  │ ...                             ││
│  └─────────────────────────────────┘│
│                                     │
│  [Previous Page] [Next Page]        │
└─────────────────────────────────────┘
```

#### 7. Receipt Generation
```
After validation:

┌─────────────────────────────────────┐
│  ACCUSE DE RECEPTION                │
├─────────────────────────────────────┤
│                                     │
│  ✓ Your liasse has been             │
│    successfully validated by        │
│    our system.                      │
│                                     │
│  Reference: TN/2026/0001234         │
│  Date: 2026-09-01                   │
│  Taxpayer: ACME SA (1234567A)       │
│  Fiscal Year: 2026                  │
│                                     │
│  [ 📥 Download Receipt ]             │
│  [ ← Back ]                         │
│                                     │
│  Note: This is a system-generated   │
│  receipt. The official tax          │
│  administration will provide        │
│  their own acknowledgement upon     │
│  submission.                        │
└─────────────────────────────────────┘
```

### UI Behavioral Rules

1. **Deposit State Reflects Available Actions**:
   ```
   Brouillon/EnSaisie/EnErreur:
   - Can upload documents
   - Can delete liasse
   - Cannot submit
   
   EnAttenteDeValidation:
   - Cannot upload
   - Cannot delete
   - Can submit
   
   Deposee:
   - Cannot modify
   - Can view
   
   Validee:
   - Cannot modify
   - Can download
   - Can generate receipt
   ```

2. **Provisional vs Definitive Info**:
   - Show banner if Provisional
   - Warn if Definitive will replace Provisional
   - Block new Provisional after Definitive

3. **Authorization-Based UI**:
   - Show only companies user is authorized for
   - Grey-out unauthorized actions
   - Clear error messages for permission failures

---

## SECTION H: EDGE CASES & VALIDATION

### Test Cases to Implement

#### Authentication & Authorization (Cases 1-5)

**1. User accesses unauthorized company**
```
User: john@example.com
User authorized for: Company A
Action: Try to access Company B

Expected: Forbid (403)
Message: "You are not authorized to access this taxpayer."
```

**2. Invalid matricule fiscal format**
```
Input: "123456" (too short)
Expected: BadRequest (400)
Message: "The fiscal identification number format is invalid. 
           Format expected: 7 digits + 1 key + optional 5 characters."
```

**3. Valid format but unknown taxpayer**
```
Input: "9999999Z" (syntactically valid, doesn't exist)
Expected: NotFound (404)
Message: "Taxpayer not found in the system. 
           Contact administrator if you believe this is an error."
```

**4. Taxpayer exists but user not authorized**
```
Taxpayer: ACME SA (1234567A) [exists in DB]
User: john@example.com [authenticated]
Authorization: None

Expected: Forbidden (403)
Message: "You are not authorized to access this taxpayer."
```

**5. Professional/Mandate access denied by mandate expiration**
```
User: tax.pro@example.com
Mandate: ACME SA (expired 2026-08-31)
Current Date: 2026-09-01

Expected: Forbidden (403)
Message: "Your authorization to act for this taxpayer has expired."
```

#### Liasse Category Validation (Cases 6-8)

**6. Wrong category for taxpayer activity**
```
Taxpayer: ACME SA [Activity: "Commerce"; Category: "Cas Général"]
Selection: "Bancaire"

Expected: BadRequest (400)
Message: "The selected Liasse category does not match the taxpayer's activity.
           Valid categories: Cas Général, Cas Général avec flux de trésorerie autorisé."
```

**7. MicroCredits not supported**
```
Selection: "MicroCredits"

Expected: BadRequest (400)
Message: "This Liasse category is not yet supported. 
           Contact system administrator."
```

**8. Category change after deposit created**
```
State: Liasse created as "Cas Général"
Action: Try to change to "Bancaire"

Expected: BadRequest (400)
Message: "Liasse category cannot be changed after deposit is created."
```

#### Fiscal Year & Dates (Cases 9-11)

**9. Invalid fiscal year range**
```
Exercise: 2026
Start: 2026-03-01
End: 2026-02-01 (before start)

Expected: BadRequest (400)
Message: "Exercise end date must be after start date."
```

**10. Exercise year mismatch with dates**
```
Exercise: 2026
Dates: 2025-01-01 to 2026-12-31

Expected: BadRequest (400) or Warning
Message: "Exercise year (2026) does not align with start date (2025).
           Confirm intentional fiscal year variance."
```

**11. Duplicate fiscal year with same Acte**
```
Existing: Liasse 2026, Spontané, Provisoire (Status: EnSaisie)
Action: Create new 2026, Spontané, Provisoire

Expected: BadRequest (400)
Message: "A provisional Liasse already exists for this fiscal year and act.
           Please complete or delete the existing one before creating a new deposit."
```

#### Document Upload Validation (Cases 12-20)

**12. Missing required document**
```
Liasse requires: F6001, F6002, F6003, F6004, F6005, F6006, F6007
Uploaded: F6001, F6002, F6003 (missing F6004, F6005, F6006, F6007)
Action: Click "Submit"

Expected: BadRequest (400)
Message: "The following required documents are missing:
           - F6004 — État de flux de trésorerie
           - F6005 — Détermination du résultat fiscal
           - F6006 — Notes et principes comptables
           - F6007 — Faits marquants"
```

**13. Invalid XML structure (schema violation)**
```
File: F6001-1234567A-2026.xml
Issue: Missing mandatory <Montant> element

Expected: BadRequest (400)
Validation Report:
{
  isValid: false,
  errors: [{
    line: 45,
    xmlElement: "Rubrique",
    message: "Missing required child element: Montant",
    source: "XSD"
  }]
}
```

**14. Taxpayer mismatch in XML header**
```
XML Header: MatriculeFiscalDeclarant = 9999999Z
Liasse Taxpayer: 1234567A

Expected: BadRequest (400)
Message: "The taxpayer in the XML header (9999999Z) does not match 
          the liasse taxpayer (1234567A)."
```

**15. Fiscal year mismatch in XML**
```
XML Header: Exercice = 2025
Liasse Exercice = 2026

Expected: BadRequest (400)
Message: "The fiscal year in the XML header (2025) does not match 
          the liasse fiscal year (2026)."
```

**16. Wrong document code in filename**
```
File: F6001-1234567A-2026.xml
Target Document Code: F6002

Expected: BadRequest (400)
Message: "The document code in the filename (F6001) does not match 
          the target document (F6002)."
```

**17. Invalid filename format**
```
File: bilan2026.xml (should be: F6001-1234567A-2026.xml)

Expected: BadRequest (400)
Message: "Invalid filename format. Expected: [CODE]-[MATRICULE]-[EXERCICE].xml
          Example: F6001-1234567A-2026.xml"
```

**18. Document already uploaded for same code**
```
Status: F6001 already uploaded and validated
Action: Upload new F6001

Expected: User can re-upload; new file replaces old
Message: "This will replace the previously uploaded F6001. Continue?"
```

**19. File size exceeds limit**
```
File: 150MB XML
Limit: 50MB

Expected: BadRequest (400)
Message: "File size (150MB) exceeds maximum allowed (50MB)."
```

**20. Invalid file type**
```
Upload: F6001.docx (should be XML)

Expected: BadRequest (400)
Message: "Invalid file type. Expected: .xml (got: .docx)"
```

#### Deposit State Transitions (Cases 21-24)

**21. Cannot delete validated deposit**
```
Liasse Status: Validée
Action: DELETE /api/liasses/{id}

Expected: Forbidden (403)
Message: "Only non-validated deposits can be deleted. 
          This deposit has been validated and cannot be modified."
```

**22. Cannot upload to validated deposit**
```
Liasse Status: Validée
Action: Upload new document

Expected: BadRequest (400)
Message: "This liasse cannot accept new documents in its current status."
```

**23. Cannot submit incomplete liasse**
```
Liasse: 3/7 documents uploaded
Action: POST /liasses/{id}/submit

Expected: BadRequest (400)
Message: "The liasse is incomplete. 4 required documents are missing."
```

**24. Concurrent deposit creation (race condition)**
```
Request 1 (T=0ms): POST /api/liasses (2026, Spontané, Provisoire)
Request 2 (T=5ms): POST /api/liasses (2026, Spontané, Provisoire)
[Both start processing simultaneously]

Expected: 
- Request 1: Success (201)
- Request 2: Conflict (409)
  Message: "A provisional liasse for this fiscal year already exists."
```

#### Provisional vs Definitive Rules (Cases 25-27)

**25. Provisional after Definitive rejected**
```
Existing: Liasse 2026, Spontané, Définitif, Status: Validée
Action: Create new 2026, Spontané, Provisoire

Expected: BadRequest (400)
Message: "A definitive liasse has already been deposited for this fiscal year.
          A provisional liasse cannot be transferred after a definitive deposit."
```

**26. Definitive replaces Provisional (OK)**
```
Existing: Liasse 2026, Spontané, Provisoire, Status: EnSaisie
Action: Create new 2026, Spontané, Définitif

Expected: Success (201)
Behavior: New definitive liasse created; old provisional remains but is not active
Message: "Definitive deposit created. Previous provisional is now superseded."
```

**27. Multiple Provisional versions rejected**
```
Existing: Liasse 2026, Spontané, Provisoire, Status: Deposee
Action: Create new 2026, Spontané, Provisoire

Expected: Conflict (409)
Message: "A provisional liasse for this fiscal year already exists.
          Please complete the first before creating another."
```

#### Rectification & Regularisation (Cases 28-30)

**28. Rectification requires prior Spontané**
```
Existing: Liasse 2026, Spontané (doesn't exist)
Action: Create new 2026, Rectification

Expected: BadRequest (400)
Message: "A rectification requires a prior spontaneous liasse for the same fiscal year."
```

**29. Multiple rectifications allowed**
```
Existing: Liasse 2026, Spontané, Validée
         Liasse 2026, Rectification, Validée
Action: Create new 2026, Rectification

Expected: Success (201)
Note: Multiple rectifications are allowed (no documented limit)
```

**30. Regularisation sequence**
```
Existing: Liasse 2025, Spontané, Validée
         Liasse 2025, Rectification, Validée
Action: Create new 2025, Régularisation

Expected: Success (201)
Note: Régularisation can exist independently or after others
      (Ordering not enforced by rule; for audit purposes)
```

#### Historical Access (Cases 31-33)

**31. Download from historical deposit**
```
Deposit: 2023, Spontané, Définitif, Status: Validée
User: john@example.com (authorized for this company)
Action: GET /api/liasses/{id}/documents/F6001/download

Expected: 200 + file
File: Original F6001-1234567A-2023.xml
```

**32. Unauthorized historical access**
```
Deposit: 2023 (Company A)
User: jane@example.com (authorized for Company B only)
Action: GET /api/liasses/{id}/download

Expected: Forbidden (403)
Message: "You are not authorized to access this taxpayer."
```

**33. Receipt only for Validated**
```
Deposit Status: EnSaisie
Action: POST /api/receipts/{depositId}/generate

Expected: BadRequest (400)
Message: "Receipt can only be generated for validated deposits."
```

#### Security & Audit (Cases 34-36)

**34. SQL injection attempt**
```
Input: matriculeFiscal = "1234567A' OR '1'='1"

Expected: BadRequest (400)
Behavior: Parameter-bound query prevents injection
Message: "Invalid matricule fiscal format."
```

**35. XXE attack in XML**
```
XML: <!DOCTYPE foo [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
     <root>&xxe;</root>

Expected: BadRequest (400)
Behavior: XML parser rejects DTD/entities
Message: "XML parsing error: External entities not allowed."
```

**36. Audit log created for sensitive action**
```
Action: User jane@example.com uploads F6001
Database: AuditLog entry created

Fields:
- UserId: (jane's ID)
- Action: "UploadDocument"
- EntityType: "DocumentFiscal"
- EntityId: (document ID)
- ContribuableId: (company ID)
- Timestamp: 2026-09-01 14:32:15Z
- Notes: "F6001-1234567A-2026.xml, size: 2.3MB"

Expected: Entry queryable by admin; user name logged, not password
```

---

## SECTION I: PRIORITY FIXES (Phased Implementation)

### PHASE 1: CRITICAL (Blocking Tax Compliance) — WEEK 1

**P1.1** Authentication Architecture
- [ ] Create IAuthenticationService abstraction
- [ ] Implement LocalAuthenticationService (current)
- [ ] Add mode configuration flag
- [ ] Display "PROTOTYPE" banner in development mode

**P1.2** User vs Taxpayer
- [ ] Create UserCompanyAuthorization model
- [ ] Add authorization checks to all endpoints
- [ ] Migrate existing User.Contribuables data
- [ ] Test access control

**P1.3** Provisional after Definitive Rule
- [ ] Implement validation in LiasseService.CreerAsync
- [ ] Add backend test cases
- [ ] Block invalid transitions

**P1.4** Authorization Enforcement
- [ ] Add [AuthorizeForCompany()] attribute to all endpoints
- [ ] Verify user permission on every request
- [ ] Return 403 for unauthorized access

**P1.5** Audit Logging
- [ ] Create AuditLog table
- [ ] Log authentication events
- [ ] Log deposit creation/deletion
- [ ] Log document upload/validation

### PHASE 2: HIGH (Workflow Correctness) — WEEK 2

**P2.1** Taxpayer Identification Workflow
- [ ] Create "Identify Taxpayer" screen
- [ ] Implement POST /api/auth/identify-taxpayer
- [ ] Verify taxpayer exists before proceeding
- [ ] Display taxpayer details

**P2.2** Deposit State Machine
- [ ] Clarify StatutLiasse semantics
- [ ] Add explicit state transition validation
- [ ] Implement non-valid deposit concept
- [ ] Test state transitions

**P2.3** Duplicate Prevention Logic
- [ ] Replace simple index with complex logic
- [ ] Handle Rectification/Régularisation
- [ ] Add test cases for all combinations

**P2.4** Verify Liasse Operation
- [ ] Create explicit verification endpoint
- [ ] Display required documents
- [ ] Calculate completeness percentage

**P2.5** Delete Non-Valid Deposit
- [ ] Enforce status-based deletion
- [ ] Prevent deletion of validated deposits
- [ ] Add audit logging

### PHASE 3: MEDIUM (Data Integrity) — WEEK 3

**P3.1** XML Validation Enhancements
- [ ] Add XXE protection
- [ ] Enhance header coherence checks
- [ ] Validate ActeDeDepot/NatureDepot match

**P3.2** File Upload Security
- [ ] Add checksum validation
- [ ] Implement path traversal protection
- [ ] Add file size enforcement
- [ ] Validate filename format

**P3.3** Historical Access
- [ ] Create comprehensive tracking endpoints
- [ ] Implement authorization checks
- [ ] Enable document download from history
- [ ] Add filter capability

**P3.4** Receipt Generation
- [ ] Restrict to Validated deposits only
- [ ] Generate unique reference
- [ ] Create PDF template
- [ ] Add logging

### PHASE 4: LOW (UI/UX) — WEEK 4

**P4.1** Dashboard Redesign
- [ ] Show company context
- [ ] Display conditional actions
- [ ] Add fiscal year selector

**P4.2** Upload Form Enhancements
- [ ] Show validation errors in real-time
- [ ] Add progress indicators
- [ ] Implement drag-and-drop

**P4.3** History Page
- [ ] Create comprehensive tracking UI
- [ ] Add filters
- [ ] Enable document download

**P4.4** User Preferences
- [ ] Remember selected company
- [ ] Store UI preferences
- [ ] Add language selection

---

## CONCLUSION & RECOMMENDATIONS

### Key Insights

1. **Strong Foundation**: The application has excellent database design and correctly implements ActeDeDepot/NatureLiasse concepts

2. **Critical Gaps**:
   - Authorization model incomplete
   - Provisional after Definitive rule missing
   - No audit logging
   - Taxpayer identification workflow missing
   - Authentication not labeled as prototype

3. **Security Considerations**:
   - XXE protection needed
   - Rate limiting recommended
   - Path traversal checks needed
   - Audit trail essential for tax compliance

4. **Timeline**: All Phase 1 + 2 fixes needed before production use (~3-4 weeks with 2-3 developers)

### Next Steps

1. Review this audit with stakeholders
2. Prioritize fixes by business need
3. Implement Phase 1 immediately
4. Create test suite for all edge cases
5. Perform security audit
6. Get sign-off from tax authority liaison

---

**Report Version**: 1.0  
**Date**: 2026-09-01  
**Status**: Draft for Review

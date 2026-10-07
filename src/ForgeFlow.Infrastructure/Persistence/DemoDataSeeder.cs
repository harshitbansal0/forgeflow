using ForgeFlow.Application.Common.Abstractions;
using ForgeFlow.Domain.Audit;
using ForgeFlow.Domain.Changes;
using ForgeFlow.Domain.Common;
using ForgeFlow.Domain.Components;
using ForgeFlow.Domain.Products;
using ForgeFlow.Domain.Revisions;
using ForgeFlow.Domain.Users;
using ForgeFlow.Domain.Workflows;

namespace ForgeFlow.Infrastructure.Persistence;

/// <summary>
/// Aerospace-themed demo data. Engineering changes are driven through the real domain workflow,
/// and the audit trail is written by hand with back-dated timestamps.
/// </summary>
public sealed class DemoDataSeeder(ForgeFlowDbContext db, IPasswordHasher passwordHasher, TimeProvider clock)
{
    public const string DemoPassword = "ForgeFlow!2026";

    private DateTime _now;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        _now = clock.GetUtcNow().UtcDateTime;
        db.AuditCaptureEnabled = false;
        try
        {
            await SeedCoreAsync(cancellationToken);
        }
        finally
        {
            db.AuditCaptureEnabled = true;
        }
    }

    private async Task SeedCoreAsync(CancellationToken ct)
    {
        // Users and workflows
        var admin = CreateUser("admin@forgeflow.local", "Claire Dubois", UserRole.Admin);
        var elena = CreateUser("engineer@forgeflow.local", "Elena Petrova", UserRole.Engineer);
        var marco = CreateUser("engineer2@forgeflow.local", "Marco Rossi", UserRole.Engineer);
        var quentin = CreateUser("approver@forgeflow.local", "Quentin Martin", UserRole.Approver);
        var hannah = CreateUser("approver2@forgeflow.local", "Hannah Schmidt", UserRole.Approver);
        var victor = CreateUser("viewer@forgeflow.local", "Victor Lee", UserRole.Viewer);
        User[] users = [admin, elena, marco, quentin, hannah, victor];
        db.Users.AddRange(users);

        var standard = CreateWorkflow(admin, "Standard Engineering Change", "Two-stage review for routine design changes.", isDefault: true,
            ("Technical Review", UserRole.Approver, 1),
            ("Change Control Board", UserRole.Admin, 1));
        var expedited = CreateWorkflow(admin, "Expedited Change", "Single sign-off for documentation and low-risk changes.", isDefault: false,
            ("Change Control Board", UserRole.Approver, 1));
        var safetyCritical = CreateWorkflow(admin, "Safety-Critical Change", "Multi-discipline review for flight-critical hardware.", isDefault: false,
            ("Engineering Peer Review", UserRole.Engineer, 1),
            ("Quality & Airworthiness Review", UserRole.Approver, 2),
            ("Change Control Board", UserRole.Admin, 1));
        WorkflowDefinition[] workflows = [standard, expedited, safetyCritical];
        db.WorkflowDefinitions.AddRange(workflows);
        await db.SaveChangesAsync(ct);

        foreach (var workflow in workflows)
        {
            Log(workflow.CreatedAtUtc, admin, AuditActions.Created, nameof(WorkflowDefinition), workflow.Id, $"Workflow '{workflow.Name}' created");
        }

        // Components (revision A of each)
        var bracket = CreateComponent(marco, 62, "CMP-1001", "Wing Rib Attach Bracket", ComponentType.Mechanical, "Al 7075-T6", "EA",
            "Aerostruct GmbH", 148.50m, LifecycleState.Production, "DWG-1001", 0.420m, "Machined bracket joining rib to front spar.");
        var fastener = CreateComponent(marco, 62, "CMP-1002", "Hi-Lok Fastener HL19PB8", ComponentType.Fastener, "Ti-6Al-4V", "EA",
            "Fastek Industries", 2.35m, LifecycleState.Production, "DWG-1002", 0.012m, "Titanium shear pin with collar.");
        var sealKit = CreateComponent(elena, 61, "CMP-1003", "Hydraulic Seal Kit", ComponentType.Hydraulic, "Viton / PTFE", "KIT",
            "HydraSeal Ltd", 86.00m, LifecycleState.Production, "DWG-1003", 0.150m, "Rod and piston seal set, 3000 psi rated.");
        var ledDriver = CreateComponent(marco, 60, "CMP-1004", "LED Driver PCB Assembly", ComponentType.Electronic, "FR-4", "EA",
            "Lumetrix Electronics", 212.00m, LifecycleState.Production, "DWG-1004", 0.085m, "28 VDC constant-current driver.");
        var harness = CreateComponent(marco, 60, "CMP-1005", "Cabin Lighting Wiring Harness", ComponentType.Electrical, "ETFE-insulated copper", "EA",
            "Harnex Systems", 340.00m, LifecycleState.Production, "DWG-1005", 1.200m, "Overhead-bin lighting harness, 12 m.");
        var rodEnd = CreateComponent(elena, 59, "CMP-1006", "Actuator Rod End", ComponentType.Mechanical, "15-5PH stainless steel", "EA",
            "Precision Motion SA", 96.75m, LifecycleState.Production, "DWG-1006", 0.310m, "Threaded rod end for flap actuator.");
        var bearing = CreateComponent(elena, 59, "CMP-1007", "Spherical Bearing", ComponentType.Mechanical, "52100 bearing steel", "EA",
            "Precision Motion SA", 54.20m, LifecycleState.Production, "DWG-1007", 0.095m, "Self-lubricating spherical plain bearing.");
        var panel = CreateComponent(marco, 40, "CMP-1008", "CFRP Access Panel", ComponentType.Mechanical, "Carbon fibre / epoxy", "EA",
            "Composite Works", 1250.00m, LifecycleState.Development, "DWG-1008", 2.400m, "Belly fairing access panel layup.");
        var firmware = CreateComponent(marco, 58, "CMP-1009", "Lighting Controller Firmware", ComponentType.Software, null, "EA",
            "In-house", null, LifecycleState.Production, "SW-1009", null, "DO-178C level D controller firmware.");
        var sealant = CreateComponent(elena, 58, "CMP-1010", "Polysulfide Sealant", ComponentType.Material, "Polysulfide, class B", "ML",
            "SealCo", 0.85m, LifecycleState.Production, null, null, "Fuel-resistant fillet and faying-surface sealant.");
        var manifoldBody = CreateComponent(marco, 20, "CMP-1011", "Manifold Block Body", ComponentType.Mechanical, "Al 2024-T351", "EA",
            "Aerostruct GmbH", 610.00m, LifecycleState.Concept, "DWG-1011", 3.800m, "Machined hydraulic manifold block.");
        var transducer = CreateComponent(elena, 15, "CMP-1012", "Pressure Transducer", ComponentType.Electronic, null, "EA",
            "SensAero", 430.00m, LifecycleState.Development, "DWG-1012", 0.060m, "0-5000 psi transducer for health monitoring.");

        Component[] components = [bracket, fastener, sealKit, ledDriver, harness, rodEnd, bearing, panel, firmware, sealant, manifoldBody, transducer];
        db.Components.AddRange(components);
        await db.SaveChangesAsync(ct);

        foreach (var component in components)
        {
            var creator = users.First(u => u.DisplayName == component.CreatedBy);
            Log(component.CreatedAtUtc, creator, AuditActions.Created, nameof(Component), component.Id, $"Component {component.PartNumber} created");
        }

        // Products (revision A with BOM)
        var actuator = CreateProduct(elena, 55, "FF-ACT-100", "Flap Actuator Assembly", "Flight Controls", LifecycleState.Production,
            "Trailing-edge flap actuator with dual load path.",
            (rodEnd, 2m, "RE1-RE2"), (bearing, 2m, "B1-B2"), (sealKit, 1m, null), (fastener, 12m, null), (sealant, 15m, null));
        var lighting = CreateProduct(marco, 54, "FF-CLM-200", "Cabin Lighting Module", "Cabin Systems", LifecycleState.Production,
            "Dimmable LED lighting module for overhead bins.",
            (ledDriver, 1m, "U1"), (harness, 1m, "W1"), (firmware, 1m, null), (fastener, 4m, null));
        var wingKit = CreateProduct(elena, 30, "FF-WRB-300", "Wing Rib Bracket Kit", "Airframe Structures", LifecycleState.Development,
            "Bracket kit for rib-to-spar attachment, inboard wing.",
            (bracket, 6m, null), (fastener, 36m, null), (sealant, 40m, null));
        var manifold = CreateProduct(marco, 18, "FF-HYD-400", "Hydraulic Manifold Assembly", "Hydraulics", LifecycleState.Concept,
            "Integrated manifold for the green hydraulic system.",
            (manifoldBody, 1m, null), (sealKit, 2m, null), (transducer, 1m, "PT1"));
        var accessPanel = CreateProduct(marco, 12, "FF-ACP-500", "Belly Fairing Access Panel", "Airframe Structures", LifecycleState.Development,
            "Removable composite access panel with fastener pattern.",
            (panel, 1m, null), (fastener, 24m, null), (sealant, 20m, null));

        Product[] products = [actuator, lighting, wingKit, manifold, accessPanel];
        db.Products.AddRange(products);
        await db.SaveChangesAsync(ct);

        foreach (var product in products)
        {
            Log(product.CreatedAtUtc, product.Owner, AuditActions.Created, nameof(Product), product.Id, $"Product {product.ProductNumber} created");
        }

        // ECO-00001: baseline release, implemented
        RevisionBase[] baselineRevisions =
        [
            actuator.GetWorkingRevision()!,
            lighting.GetWorkingRevision()!,
            .. new[] { fastener, sealKit, ledDriver, harness, rodEnd, bearing, firmware, sealant, bracket }.Select(c => c.GetWorkingRevision()!)
        ];
        var baseline = CreateChange(elena, standard, 46, ChangePriority.Medium,
            "Initial release of flap actuator and cabin lighting baselines",
            "Release revision A of the flap actuator, the cabin lighting module and their production components.",
            "First article inspection passed; configuration baseline required for production.",
            baselineRevisions);
        await db.SaveChangesAsync(ct);
        Submit(baseline, standard, elena, 45);
        Decide(baseline, quentin, ApprovalOutcome.Approved, "Design review complete; stress margins verified.", 44);
        Decide(baseline, admin, ApprovalOutcome.Approved, "Approved by the change control board.", 43);
        Implement(baseline, elena, 42);
        await db.SaveChangesAsync(ct);

        // ECO-00002: rejected fastener material change
        var steelFastener = fastener.Revise("Alternative steel fastener to reduce unit cost.");
        steelFastener.WeightKg = 0.021m;
        Stamp(steelFastener, marco, 12);
        var fastenerChange = CreateChange(marco, expedited, 11, ChangePriority.Low,
            "Replace titanium fasteners with steel",
            "Switch HL19PB8 fasteners from Ti-6Al-4V to A286 steel to reduce procurement cost.",
            "Supplier lead time and cost reduction initiative.",
            [steelFastener]);
        await db.SaveChangesAsync(ct);
        Submit(fastenerChange, expedited, marco, 10);
        Decide(fastenerChange, quentin, ApprovalOutcome.Rejected, "Weight penalty of 1.8 kg per wing is not acceptable.", 9);
        await db.SaveChangesAsync(ct);

        // ECO-00003: actuator bearing upgrade, waiting for the change control board
        var actuatorB = actuator.Revise("Add redundant spherical bearings and a health-monitoring transducer.");
        Stamp(actuatorB, elena, 5);
        var bearingLine = actuatorB.BomItems.First(i => i.ComponentId == bearing.Id);
        actuatorB.UpdateBomItem(bearingLine, 4m, "B1-B4", "Second bearing per rod end.");
        actuatorB.AddBomItem(transducer, 1m, "PT1", "Pressure transducer for actuator health monitoring.");
        var bearingChange = CreateChange(elena, standard, 4, ChangePriority.High,
            "Add redundant bearing to flap actuator",
            "Fatigue testing showed bearing wear beyond limits at 60k cycles. Add a second spherical bearing per rod end and a pressure transducer for health monitoring.",
            "Fatigue test report FTR-2291.",
            [actuatorB]);
        await db.SaveChangesAsync(ct);
        Submit(bearingChange, standard, elena, 3);
        Decide(bearingChange, quentin, ApprovalOutcome.Approved, "Fatigue analysis reviewed; change is justified.", 2);
        await db.SaveChangesAsync(ct);

        // ECO-00004: safety-critical harness change, waiting for engineering peer review
        var harnessB = harness.Revise("Insulation upgraded to meet revised flammability requirements.");
        harnessB.WeightKg = 1.150m;
        Stamp(harnessB, elena, 3);
        var harnessChange = CreateChange(elena, safetyCritical, 2, ChangePriority.Critical,
            "Upgrade cabin harness insulation",
            "Replace harness insulation with a low-smoke, halogen-free compound across all overhead lighting runs.",
            "Revised cabin flammability requirements effective next quarter.",
            [harnessB]);
        await db.SaveChangesAsync(ct);
        Submit(harnessChange, safetyCritical, elena, 1);
        await db.SaveChangesAsync(ct);

        // ECO-00005: draft release request for the wing bracket kit
        var wingKitChange = CreateChange(elena, standard, 1, ChangePriority.Medium,
            "Wing rib bracket kit initial release",
            "Release revision A of the inboard wing rib bracket kit for the qualification build.",
            "Qualification build scheduled; released configuration required.",
            [wingKit.GetWorkingRevision()!]);
        await db.SaveChangesAsync(ct);

        foreach (var change in new[] { baseline, fastenerChange, bearingChange, harnessChange, wingKitChange })
        {
            Log(change.CreatedAtUtc, change.RequestedBy, AuditActions.Created, nameof(EngineeringChange), change.Id, $"{change.ChangeNumber} created");
        }

        await db.SaveChangesAsync(ct);
    }

    private DateTime DaysAgo(double days) => _now.AddDays(-days);

    private User CreateUser(string email, string displayName, UserRole role) => new()
    {
        Email = email,
        DisplayName = displayName,
        Role = role,
        IsActive = true,
        PasswordHash = passwordHasher.Hash(DemoPassword),
        CreatedAtUtc = DaysAgo(120),
        CreatedBy = ForgeFlowDbContext.SystemActor
    };

    private WorkflowDefinition CreateWorkflow(
        User createdBy, string name, string description, bool isDefault, params (string Name, UserRole Role, int Approvals)[] steps) => new()
    {
        Name = name,
        Description = description,
        IsActive = true,
        IsDefault = isDefault,
        CreatedAtUtc = DaysAgo(100),
        CreatedBy = createdBy.DisplayName,
        Steps = steps
            .Select((step, index) => new WorkflowStep
            {
                StepOrder = index + 1,
                Name = step.Name,
                ApproverRole = step.Role,
                RequiredApprovals = step.Approvals
            })
            .ToList()
    };

    private Component CreateComponent(
        User createdBy, double daysAgo, string partNumber, string name, ComponentType type, string? material, string unitOfMeasure,
        string supplier, decimal? unitCost, LifecycleState lifecycleState, string? drawingNumber, decimal? weightKg, string description)
    {
        var component = new Component
        {
            PartNumber = partNumber,
            Name = name,
            Description = description,
            Type = type,
            Material = material,
            UnitOfMeasure = unitOfMeasure,
            Supplier = supplier,
            UnitCost = unitCost,
            LifecycleState = lifecycleState,
            CreatedAtUtc = DaysAgo(daysAgo),
            CreatedBy = createdBy.DisplayName
        };
        var revision = component.Revise("Initial revision");
        revision.DrawingNumber = drawingNumber;
        revision.WeightKg = weightKg;
        Stamp(revision, createdBy, daysAgo);
        return component;
    }

    private Product CreateProduct(
        User owner, double daysAgo, string productNumber, string name, string category, LifecycleState lifecycleState,
        string description, params (Component Component, decimal Quantity, string? ReferenceDesignator)[] bom)
    {
        var product = new Product
        {
            ProductNumber = productNumber,
            Name = name,
            Description = description,
            Category = category,
            LifecycleState = lifecycleState,
            Owner = owner,
            OwnerId = owner.Id,
            CreatedAtUtc = DaysAgo(daysAgo),
            CreatedBy = owner.DisplayName
        };
        var revision = product.Revise("Initial revision");
        Stamp(revision, owner, daysAgo);
        foreach (var (component, quantity, referenceDesignator) in bom)
        {
            revision.AddBomItem(component, quantity, referenceDesignator, null);
        }

        return product;
    }

    private EngineeringChange CreateChange(
        User requester, WorkflowDefinition workflow, double daysAgo, ChangePriority priority, string title, string description,
        string reason, IEnumerable<RevisionBase> revisions)
    {
        var change = new EngineeringChange
        {
            Title = title,
            Description = description,
            Reason = reason,
            Priority = priority,
            RequestedBy = requester,
            RequestedById = requester.Id,
            WorkflowDefinition = workflow,
            WorkflowDefinitionId = workflow.Id,
            CreatedAtUtc = DaysAgo(daysAgo),
            CreatedBy = requester.DisplayName
        };
        foreach (var revision in revisions)
        {
            change.AddAffectedRevision(revision, null);
        }

        db.EngineeringChanges.Add(change);
        return change;
    }

    private void Submit(EngineeringChange change, WorkflowDefinition workflow, User requester, double daysAgo)
    {
        change.Submit(workflow.Steps, DaysAgo(daysAgo));
        Log(DaysAgo(daysAgo), requester, AuditActions.Submitted, nameof(EngineeringChange), change.Id,
            $"{change.ChangeNumber} submitted for approval via '{workflow.Name}' ({workflow.Steps.Count} steps)");
    }

    private void Decide(EngineeringChange change, User approver, ApprovalOutcome outcome, string comment, double daysAgo)
    {
        var stepName = change.GetActiveStep()!.Name;
        var result = change.RecordDecision(approver.Id, approver.Role, outcome, comment, DaysAgo(daysAgo));
        var summary = result switch
        {
            DecisionResult.ChangeRejected => $"{change.ChangeNumber} rejected at '{stepName}' by {approver.DisplayName}: {comment}",
            DecisionResult.ChangeApproved => $"{change.ChangeNumber} approved at '{stepName}' by {approver.DisplayName}; all approval steps complete",
            DecisionResult.StepAdvanced => $"{change.ChangeNumber} approved at '{stepName}' by {approver.DisplayName}; moved to '{change.GetActiveStep()!.Name}'",
            _ => $"{change.ChangeNumber} approved at '{stepName}' by {approver.DisplayName}; awaiting further approvals"
        };
        Log(DaysAgo(daysAgo), approver, outcome == ApprovalOutcome.Approved ? AuditActions.Approved : AuditActions.Rejected,
            nameof(EngineeringChange), change.Id, summary);
    }

    private void Implement(EngineeringChange change, User implementer, double daysAgo)
    {
        var released = change.Implement(implementer.DisplayName, DaysAgo(daysAgo));
        Log(DaysAgo(daysAgo), implementer, AuditActions.Implemented, nameof(EngineeringChange), change.Id,
            $"{change.ChangeNumber} implemented; {released.Count} revision(s) released");

        foreach (var revision in released)
        {
            var (type, parentType, parentId, label) = revision switch
            {
                ProductRevision p => (nameof(ProductRevision), nameof(Product), p.ProductId, $"{p.Product.ProductNumber} Rev {p.RevisionCode}"),
                ComponentRevision c => (nameof(ComponentRevision), nameof(Component), c.ComponentId, $"{c.Component.PartNumber} Rev {c.RevisionCode}"),
                _ => throw new InvalidOperationException($"Unexpected revision type {revision.GetType().Name}.")
            };
            Log(DaysAgo(daysAgo), implementer, AuditActions.Released, type, revision.Id, $"{label} released by {change.ChangeNumber}", parentType, parentId);
        }
    }

    private void Stamp(AuditableEntity entity, User user, double daysAgo)
    {
        entity.CreatedAtUtc = DaysAgo(daysAgo);
        entity.CreatedBy = user.DisplayName;
    }

    private void Log(
        DateTime timestampUtc, User user, string action, string entityType, int entityId, string summary,
        string? parentEntityType = null, int? parentEntityId = null) =>
        db.AuditLogs.Add(new AuditLog
        {
            TimestampUtc = timestampUtc,
            UserId = user.Id,
            UserName = user.DisplayName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            ParentEntityType = parentEntityType,
            ParentEntityId = parentEntityId?.ToString(),
            Summary = summary
        });
}

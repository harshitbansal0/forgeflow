IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [TimestampUtc] datetime2 NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(256) NOT NULL,
        [Action] nvarchar(40) NOT NULL,
        [EntityType] nvarchar(60) NOT NULL,
        [EntityId] nvarchar(40) NOT NULL,
        [ParentEntityType] nvarchar(60) NULL,
        [ParentEntityId] nvarchar(40) NULL,
        [Summary] nvarchar(1000) NULL,
        [ChangesJson] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE TABLE [Components] (
        [Id] int NOT NULL IDENTITY,
        [PartNumber] nvarchar(40) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Type] nvarchar(20) NOT NULL,
        [Material] nvarchar(100) NULL,
        [UnitOfMeasure] nvarchar(10) NOT NULL,
        [Supplier] nvarchar(200) NULL,
        [UnitCost] decimal(18,2) NULL,
        [LifecycleState] nvarchar(20) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_Components] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [Email] nvarchar(256) NOT NULL,
        [DisplayName] nvarchar(120) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [LastLoginAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE TABLE [WorkflowDefinitions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(120) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL,
        [IsDefault] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_WorkflowDefinitions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [ProductNumber] nvarchar(40) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Category] nvarchar(100) NOT NULL,
        [LifecycleState] nvarchar(20) NOT NULL,
        [OwnerId] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE TABLE [EngineeringChanges] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [Reason] nvarchar(2000) NULL,
        [Priority] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [RequestedById] int NOT NULL,
        [WorkflowDefinitionId] int NOT NULL,
        [CurrentStepOrder] int NULL,
        [SubmittedAtUtc] datetime2 NULL,
        [DecidedAtUtc] datetime2 NULL,
        [ImplementedAtUtc] datetime2 NULL,
        [LastActivityAtUtc] datetime2 NULL,
        [Version] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(256) NULL,
        CONSTRAINT [PK_EngineeringChanges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EngineeringChanges_Users_RequestedById] FOREIGN KEY ([RequestedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EngineeringChanges_WorkflowDefinitions_WorkflowDefinitionId] FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [WorkflowDefinitions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE TABLE [WorkflowSteps] (
        [Id] int NOT NULL IDENTITY,
        [WorkflowDefinitionId] int NOT NULL,
        [StepOrder] int NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [ApproverRole] nvarchar(20) NOT NULL,
        [RequiredApprovals] int NOT NULL,
        CONSTRAINT [PK_WorkflowSteps] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowSteps_WorkflowDefinitions_WorkflowDefinitionId] FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [WorkflowDefinitions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE TABLE [ChangeApprovalSteps] (
        [Id] int NOT NULL IDENTITY,
        [EngineeringChangeId] int NOT NULL,
        [StepOrder] int NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [ApproverRole] nvarchar(20) NOT NULL,
        [RequiredApprovals] int NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [ActivatedAtUtc] datetime2 NULL,
        [CompletedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ChangeApprovalSteps] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChangeApprovalSteps_EngineeringChanges_EngineeringChangeId] FOREIGN KEY ([EngineeringChangeId]) REFERENCES [EngineeringChanges] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE TABLE [ComponentRevisions] (
        [Id] int NOT NULL IDENTITY,
        [ComponentId] int NOT NULL,
        [DrawingNumber] nvarchar(60) NULL,
        [WeightKg] decimal(10,3) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(256) NULL,
        [RevisionCode] nvarchar(4) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [ChangeSummary] nvarchar(1000) NULL,
        [ReleasedAtUtc] datetime2 NULL,
        [ReleasedBy] nvarchar(256) NULL,
        [EngineeringChangeId] int NULL,
        CONSTRAINT [PK_ComponentRevisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ComponentRevisions_Components_ComponentId] FOREIGN KEY ([ComponentId]) REFERENCES [Components] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ComponentRevisions_EngineeringChanges_EngineeringChangeId] FOREIGN KEY ([EngineeringChangeId]) REFERENCES [EngineeringChanges] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductRevisions] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        [UpdatedBy] nvarchar(256) NULL,
        [RevisionCode] nvarchar(4) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [ChangeSummary] nvarchar(1000) NULL,
        [ReleasedAtUtc] datetime2 NULL,
        [ReleasedBy] nvarchar(256) NULL,
        [EngineeringChangeId] int NULL,
        CONSTRAINT [PK_ProductRevisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductRevisions_EngineeringChanges_EngineeringChangeId] FOREIGN KEY ([EngineeringChangeId]) REFERENCES [EngineeringChanges] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductRevisions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE TABLE [ApprovalDecisions] (
        [Id] int NOT NULL IDENTITY,
        [ChangeApprovalStepId] int NOT NULL,
        [ApproverId] int NOT NULL,
        [Outcome] nvarchar(20) NOT NULL,
        [Comment] nvarchar(2000) NULL,
        [DecidedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ApprovalDecisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ApprovalDecisions_ChangeApprovalSteps_ChangeApprovalStepId] FOREIGN KEY ([ChangeApprovalStepId]) REFERENCES [ChangeApprovalSteps] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ApprovalDecisions_Users_ApproverId] FOREIGN KEY ([ApproverId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE TABLE [BomItems] (
        [Id] int NOT NULL IDENTITY,
        [ProductRevisionId] int NOT NULL,
        [ComponentId] int NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [ReferenceDesignator] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_BomItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BomItems_Components_ComponentId] FOREIGN KEY ([ComponentId]) REFERENCES [Components] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BomItems_ProductRevisions_ProductRevisionId] FOREIGN KEY ([ProductRevisionId]) REFERENCES [ProductRevisions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE TABLE [ChangeAffectedItems] (
        [Id] int NOT NULL IDENTITY,
        [EngineeringChangeId] int NOT NULL,
        [ProductRevisionId] int NULL,
        [ComponentRevisionId] int NULL,
        [Note] nvarchar(500) NULL,
        CONSTRAINT [PK_ChangeAffectedItems] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ChangeAffectedItems_OneRevision] CHECK ((ProductRevisionId IS NOT NULL AND ComponentRevisionId IS NULL) OR (ProductRevisionId IS NULL AND ComponentRevisionId IS NOT NULL)),
        CONSTRAINT [FK_ChangeAffectedItems_ComponentRevisions_ComponentRevisionId] FOREIGN KEY ([ComponentRevisionId]) REFERENCES [ComponentRevisions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ChangeAffectedItems_EngineeringChanges_EngineeringChangeId] FOREIGN KEY ([EngineeringChangeId]) REFERENCES [EngineeringChanges] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ChangeAffectedItems_ProductRevisions_ProductRevisionId] FOREIGN KEY ([ProductRevisionId]) REFERENCES [ProductRevisions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ApprovalDecisions_ApproverId] ON [ApprovalDecisions] ([ApproverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ApprovalDecisions_ChangeApprovalStepId_ApproverId] ON [ApprovalDecisions] ([ChangeApprovalStepId], [ApproverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityType_EntityId] ON [AuditLogs] ([EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ParentEntityType_ParentEntityId] ON [AuditLogs] ([ParentEntityType], [ParentEntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_TimestampUtc] ON [AuditLogs] ([TimestampUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BomItems_ComponentId] ON [BomItems] ([ComponentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BomItems_ProductRevisionId_ComponentId] ON [BomItems] ([ProductRevisionId], [ComponentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ChangeAffectedItems_ComponentRevisionId] ON [ChangeAffectedItems] ([ComponentRevisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ChangeAffectedItems_EngineeringChangeId_ComponentRevisionId] ON [ChangeAffectedItems] ([EngineeringChangeId], [ComponentRevisionId]) WHERE [ComponentRevisionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ChangeAffectedItems_EngineeringChangeId_ProductRevisionId] ON [ChangeAffectedItems] ([EngineeringChangeId], [ProductRevisionId]) WHERE [ProductRevisionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ChangeAffectedItems_ProductRevisionId] ON [ChangeAffectedItems] ([ProductRevisionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChangeApprovalSteps_EngineeringChangeId_StepOrder] ON [ChangeApprovalSteps] ([EngineeringChangeId], [StepOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ChangeApprovalSteps_Status_ApproverRole] ON [ChangeApprovalSteps] ([Status], [ApproverRole]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ComponentRevisions_ComponentId_RevisionCode] ON [ComponentRevisions] ([ComponentId], [RevisionCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ComponentRevisions_EngineeringChangeId] ON [ComponentRevisions] ([EngineeringChangeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ComponentRevisions_Status] ON [ComponentRevisions] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Components_LifecycleState] ON [Components] ([LifecycleState]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Components_Name] ON [Components] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Components_PartNumber] ON [Components] ([PartNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Components_Type] ON [Components] ([Type]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EngineeringChanges_RequestedById] ON [EngineeringChanges] ([RequestedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EngineeringChanges_Status] ON [EngineeringChanges] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_EngineeringChanges_WorkflowDefinitionId] ON [EngineeringChanges] ([WorkflowDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductRevisions_EngineeringChangeId] ON [ProductRevisions] ([EngineeringChangeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductRevisions_ProductId_RevisionCode] ON [ProductRevisions] ([ProductId], [RevisionCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductRevisions_Status] ON [ProductRevisions] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_Category] ON [Products] ([Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_LifecycleState] ON [Products] ([LifecycleState]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_Name] ON [Products] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_OwnerId] ON [Products] ([OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Products_ProductNumber] ON [Products] ([ProductNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WorkflowDefinitions_Name] ON [WorkflowDefinitions] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_WorkflowSteps_WorkflowDefinitionId_StepOrder] ON [WorkflowSteps] ([WorkflowDefinitionId], [StepOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261007064513_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261007064513_InitialCreate', N'9.0.20');
END;

COMMIT;
GO


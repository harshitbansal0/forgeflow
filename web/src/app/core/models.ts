export type UserRole = 'Viewer' | 'Engineer' | 'Approver' | 'Admin';
export type LifecycleState = 'Concept' | 'Development' | 'Production' | 'Obsolete';
export type RevisionStatus = 'Draft' | 'InReview' | 'Released' | 'Superseded';
export type ComponentType = 'Mechanical' | 'Electrical' | 'Electronic' | 'Hydraulic' | 'Software' | 'Fastener' | 'Material';
export type ChangeStatus = 'Draft' | 'InReview' | 'Approved' | 'Rejected' | 'Implemented' | 'Cancelled';
export type ChangePriority = 'Low' | 'Medium' | 'High' | 'Critical';
export type ApprovalStepStatus = 'Pending' | 'Active' | 'Approved' | 'Rejected' | 'Skipped';
export type ApprovalOutcome = 'Approved' | 'Rejected';
export type AffectedItemType = 'Product' | 'Component';
export type BomDifferenceKind = 'Added' | 'Removed' | 'QuantityChanged';

export const USER_ROLES: UserRole[] = ['Viewer', 'Engineer', 'Approver', 'Admin'];
export const APPROVER_ROLES: UserRole[] = ['Engineer', 'Approver', 'Admin'];
export const LIFECYCLE_STATES: LifecycleState[] = ['Concept', 'Development', 'Production', 'Obsolete'];
export const COMPONENT_TYPES: ComponentType[] = ['Mechanical', 'Electrical', 'Electronic', 'Hydraulic', 'Software', 'Fastener', 'Material'];
export const CHANGE_STATUSES: ChangeStatus[] = ['Draft', 'InReview', 'Approved', 'Rejected', 'Implemented', 'Cancelled'];
export const CHANGE_PRIORITIES: ChangePriority[] = ['Low', 'Medium', 'High', 'Critical'];

/** Same rule as the API: 3-40 letters, digits or hyphens. */
export const ITEM_NUMBER_PATTERN = /^[A-Za-z0-9][A-Za-z0-9-]{2,39}$/;

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export function emptyPage<T>(): PagedResult<T> {
  return { items: [], page: 1, pageSize: 20, totalCount: 0 };
}

// ---------- Auth & users ----------
export interface UserInfo {
  id: number;
  email: string;
  displayName: string;
  role: UserRole;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: UserInfo;
}

export interface UserSummary {
  id: number;
  email: string;
  displayName: string;
  role: UserRole;
  isActive: boolean;
  lastLoginAtUtc: string | null;
  createdAtUtc: string;
}

export interface DirectoryEntry {
  id: number;
  displayName: string;
  role: UserRole;
}

export interface CreateUserInput {
  email: string;
  displayName: string;
  role: UserRole;
  password: string;
}

export interface UpdateUserInput {
  displayName: string;
  role: UserRole;
  isActive: boolean;
}

// ---------- Revisions ----------
export interface Revision {
  id: number;
  revisionCode: string;
  status: RevisionStatus;
  changeSummary: string | null;
  drawingNumber: string | null;
  weightKg: number | null;
  createdAtUtc: string;
  createdBy: string;
  releasedAtUtc: string | null;
  releasedBy: string | null;
  releasedByChangeId: number | null;
  releasedByChangeNumber: string | null;
  openChangeId: number | null;
  openChangeNumber: string | null;
}

// ---------- Products ----------
export interface ProductSummary {
  id: number;
  productNumber: string;
  name: string;
  category: string;
  lifecycleState: LifecycleState;
  ownerName: string;
  releasedRevision: string | null;
  workingRevision: string | null;
  workingRevisionStatus: RevisionStatus | null;
  updatedAtUtc: string;
}

export interface ProductDetail {
  id: number;
  productNumber: string;
  name: string;
  description: string | null;
  category: string;
  lifecycleState: LifecycleState;
  ownerId: number;
  ownerName: string;
  createdAtUtc: string;
  createdBy: string;
  updatedAtUtc: string | null;
  updatedBy: string | null;
  revisions: Revision[];
}

export interface BomItem {
  id: number;
  componentId: number;
  partNumber: string;
  componentName: string;
  componentType: ComponentType;
  unitOfMeasure: string;
  quantity: number;
  referenceDesignator: string | null;
  notes: string | null;
  componentReleasedRevision: string | null;
  componentLifecycleState: LifecycleState;
}

export interface ProductRevisionDetail {
  productId: number;
  productNumber: string;
  productName: string;
  revision: Revision;
  bomItems: BomItem[];
}

export interface BomDifference {
  componentId: number;
  partNumber: string;
  componentName: string;
  kind: BomDifferenceKind;
  fromQuantity: number | null;
  toQuantity: number | null;
}

export interface RevisionComparison {
  fromRevisionId: number;
  fromRevision: string;
  toRevisionId: number;
  toRevision: string;
  differences: BomDifference[];
}

export interface ProductInput {
  productNumber?: string;
  name: string;
  description: string | null;
  category: string;
  lifecycleState: LifecycleState;
  ownerId?: number | null;
}

export interface BomItemInput {
  componentId?: number;
  quantity: number;
  referenceDesignator: string | null;
  notes: string | null;
}

// ---------- Components ----------
export interface ComponentSummary {
  id: number;
  partNumber: string;
  name: string;
  type: ComponentType;
  material: string | null;
  unitOfMeasure: string;
  supplier: string | null;
  unitCost: number | null;
  lifecycleState: LifecycleState;
  releasedRevision: string | null;
  workingRevision: string | null;
  workingRevisionStatus: RevisionStatus | null;
  updatedAtUtc: string;
}

export interface ComponentDetail {
  id: number;
  partNumber: string;
  name: string;
  description: string | null;
  type: ComponentType;
  material: string | null;
  unitOfMeasure: string;
  supplier: string | null;
  unitCost: number | null;
  lifecycleState: LifecycleState;
  createdAtUtc: string;
  createdBy: string;
  updatedAtUtc: string | null;
  updatedBy: string | null;
  revisions: Revision[];
}

export interface WhereUsed {
  productId: number;
  productNumber: string;
  productName: string;
  revisionId: number;
  revisionCode: string;
  revisionStatus: RevisionStatus;
  quantity: number;
  referenceDesignator: string | null;
}

export interface ComponentInput {
  partNumber?: string;
  name: string;
  description: string | null;
  type: ComponentType;
  material: string | null;
  unitOfMeasure: string;
  supplier: string | null;
  unitCost: number | null;
  lifecycleState: LifecycleState;
  drawingNumber?: string | null;
  weightKg?: number | null;
}

export interface ComponentRevisionInput {
  changeSummary: string | null;
  drawingNumber: string | null;
  weightKg: number | null;
}

// ---------- Engineering changes ----------
export interface ChangeSummary {
  id: number;
  changeNumber: string;
  title: string;
  status: ChangeStatus;
  priority: ChangePriority;
  requestedBy: string;
  workflowName: string;
  affectedItemCount: number;
  currentStepName: string | null;
  createdAtUtc: string;
  submittedAtUtc: string | null;
  updatedAtUtc: string;
}

export interface AffectedItem {
  id: number;
  itemType: AffectedItemType;
  itemId: number;
  itemNumber: string;
  itemName: string;
  revisionId: number;
  revisionCode: string;
  revisionStatus: RevisionStatus;
  note: string | null;
}

export interface ApprovalDecision {
  id: number;
  approverName: string;
  outcome: ApprovalOutcome;
  comment: string | null;
  decidedAtUtc: string;
}

export interface ApprovalStep {
  id: number;
  stepOrder: number;
  name: string;
  approverRole: UserRole;
  requiredApprovals: number;
  status: ApprovalStepStatus;
  activatedAtUtc: string | null;
  completedAtUtc: string | null;
  decisions: ApprovalDecision[];
}

export interface ChangeActions {
  canEdit: boolean;
  canSubmit: boolean;
  canApprove: boolean;
  canImplement: boolean;
  canCancel: boolean;
}

export interface ChangeDetail {
  id: number;
  changeNumber: string;
  title: string;
  description: string;
  reason: string | null;
  status: ChangeStatus;
  priority: ChangePriority;
  requestedById: number;
  requestedBy: string;
  workflowDefinitionId: number;
  workflowName: string;
  currentStepOrder: number | null;
  createdAtUtc: string;
  submittedAtUtc: string | null;
  decidedAtUtc: string | null;
  implementedAtUtc: string | null;
  affectedItems: AffectedItem[];
  approvalSteps: ApprovalStep[];
  availableActions: ChangeActions;
}

export interface AffectedItemInput {
  itemType: AffectedItemType;
  itemId: number;
  note: string | null;
}

export interface ChangeInput {
  title: string;
  description: string;
  reason: string | null;
  priority: ChangePriority;
  workflowDefinitionId: number | null;
}

export interface CreateChangeInput extends ChangeInput {
  affectedItems: AffectedItemInput[];
}

export interface PendingApproval {
  changeId: number;
  changeNumber: string;
  title: string;
  priority: ChangePriority;
  requestedBy: string;
  stepName: string;
  stepOrder: number;
  totalSteps: number;
  approvalsReceived: number;
  requiredApprovals: number;
  submittedAtUtc: string | null;
  stepActivatedAtUtc: string | null;
}

// ---------- Workflows ----------
export interface WorkflowStep {
  id: number;
  stepOrder: number;
  name: string;
  approverRole: UserRole;
  requiredApprovals: number;
}

export interface Workflow {
  id: number;
  name: string;
  description: string | null;
  isActive: boolean;
  isDefault: boolean;
  steps: WorkflowStep[];
  usageCount: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface WorkflowInput {
  name: string;
  description: string | null;
  isActive: boolean;
  isDefault: boolean;
  steps: { name: string; approverRole: UserRole; requiredApprovals: number }[];
}

// ---------- Audit ----------
export interface AuditPropertyChange {
  property: string;
  oldValue: string | null;
  newValue: string | null;
}

export interface AuditLogEntry {
  id: number;
  timestampUtc: string;
  userId: number | null;
  userName: string;
  action: string;
  entityType: string;
  entityId: string;
  parentEntityType: string | null;
  parentEntityId: string | null;
  summary: string | null;
  changes: AuditPropertyChange[];
}

export const AUDIT_ACTIONS = [
  'Created', 'Updated', 'Deleted', 'Submitted', 'Approved', 'Rejected', 'Implemented', 'Released', 'Cancelled', 'LoginSucceeded', 'LoginFailed'
];

export const AUDIT_ENTITY_TYPES = [
  'Product', 'ProductRevision', 'BomItem', 'Component', 'ComponentRevision', 'EngineeringChange', 'ChangeAffectedItem',
  'WorkflowDefinition', 'WorkflowStep', 'User'
];

// ---------- Dashboard & search ----------
export interface CountByKey {
  key: string;
  count: number;
}

export interface RecentRelease {
  itemType: AffectedItemType;
  itemId: number;
  itemNumber: string;
  itemName: string;
  revisionCode: string;
  releasedAtUtc: string;
  releasedBy: string | null;
}

export interface Activity {
  id: number;
  timestampUtc: string;
  userName: string;
  action: string;
  entityType: string;
  entityId: string;
  summary: string | null;
}

export interface Dashboard {
  productCount: number;
  componentCount: number;
  openChangeCount: number;
  pendingApprovalCount: number;
  productsByLifecycle: CountByKey[];
  changesByStatus: CountByKey[];
  recentReleases: RecentRelease[];
  recentActivity: Activity[];
}

export interface SearchResult {
  type: 'Product' | 'Component' | 'EngineeringChange';
  id: number;
  number: string;
  title: string;
  status: string;
}

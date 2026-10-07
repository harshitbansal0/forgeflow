import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, numberAttribute, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { Observable, filter, switchMap } from 'rxjs';
import { WorkflowsApi } from '../../core/api/admin.api';
import { ChangesApi } from '../../core/api/changes.api';
import { AuditLogEntry, AffectedItem, ChangeDetail, Workflow } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { openComment, openConfirm } from '../../shared/dialogs';
import { HistoryTimeline } from '../../shared/history-timeline';
import { EnumLabelPipe, StatusChip } from '../../shared/status-chip';
import { ChangeEditDialog } from './change-edit-dialog';
import { ItemPickerData, ItemPickerDialog, PickedItem } from './item-picker-dialog';

@Component({
  selector: 'app-change-detail',
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    MatTooltipModule,
    EnumLabelPipe,
    StatusChip,
    HistoryTimeline
  ],
  templateUrl: './change-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangeDetailPage {
  readonly id = input.required({ transform: numberAttribute });

  private readonly api = inject(ChangesApi);
  private readonly workflowsApi = inject(WorkflowsApi);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);

  protected readonly change = signal<ChangeDetail | null>(null);
  protected readonly history = signal<AuditLogEntry[]>([]);
  protected readonly plannedWorkflow = signal<Workflow | null>(null);
  protected readonly busy = signal(false);
  protected readonly itemColumns = computed(() => {
    const columns = ['itemType', 'itemNumber', 'itemName', 'revision', 'note'];
    return this.change()?.availableActions.canEdit ? [...columns, 'actions'] : columns;
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => this.load(id));
    });
  }

  protected edit(): void {
    const change = this.change();
    if (change) {
      this.dialog
        .open<ChangeEditDialog, ChangeDetail, ChangeDetail>(ChangeEditDialog, { data: change, width: '640px' })
        .afterClosed()
        .pipe(filter((updated): updated is ChangeDetail => !!updated))
        .subscribe(updated => this.apply(updated, 'Change updated.'));
    }
  }

  protected addItem(): void {
    const change = this.change();
    if (!change) {
      return;
    }

    this.dialog
      .open<ItemPickerDialog, ItemPickerData, PickedItem>(ItemPickerDialog, {
        data: { excluded: change.affectedItems.map(i => ({ itemType: i.itemType, itemId: i.itemId })) },
        width: '560px'
      })
      .afterClosed()
      .pipe(
        filter((item): item is PickedItem => !!item),
        switchMap(item => this.api.addAffectedItem(change.id, { itemType: item.itemType, itemId: item.itemId, note: item.note }))
      )
      .subscribe(updated => this.apply(updated, 'Affected item added.'));
  }

  protected removeItem(item: AffectedItem): void {
    const change = this.change();
    if (change) {
      this.run(this.api.removeAffectedItem(change.id, item.id), `${item.itemNumber} removed from the change.`);
    }
  }

  protected submit(): void {
    const change = this.change();
    if (!change) {
      return;
    }

    openConfirm(this.dialog, {
      title: `Submit ${change.changeNumber}?`,
      message: `The affected revisions are locked and the "${change.workflowName}" approval workflow starts.`,
      confirmText: 'Submit for approval'
    })
      .pipe(filter(Boolean))
      .subscribe(() => this.run(this.api.submit(change.id), `${change.changeNumber} submitted for approval.`));
  }

  protected approve(): void {
    const change = this.change();
    if (!change) {
      return;
    }

    openComment(this.dialog, {
      title: `Approve ${change.changeNumber}`,
      message: `You are approving step "${this.activeStepName()}".`,
      label: 'Comment (optional)',
      confirmText: 'Approve',
      required: false
    }).subscribe(comment => this.run(this.api.approve(change.id, comment || null), 'Approval recorded.'));
  }

  protected reject(): void {
    const change = this.change();
    if (!change) {
      return;
    }

    openComment(this.dialog, {
      title: `Reject ${change.changeNumber}`,
      message: 'Rejecting closes the change and returns its revisions to draft.',
      label: 'Reason for rejection',
      confirmText: 'Reject',
      required: true,
      danger: true
    }).subscribe(comment => this.run(this.api.reject(change.id, comment), `${change.changeNumber} rejected.`));
  }

  protected implement(): void {
    const change = this.change();
    if (!change) {
      return;
    }

    openConfirm(this.dialog, {
      title: `Implement ${change.changeNumber}?`,
      message: 'The affected revisions will be released and their previous releases superseded.',
      confirmText: 'Implement and release'
    })
      .pipe(filter(Boolean))
      .subscribe(() => this.run(this.api.implement(change.id), `${change.changeNumber} implemented; revisions released.`));
  }

  protected cancel(): void {
    const change = this.change();
    if (!change) {
      return;
    }

    openComment(this.dialog, {
      title: `Cancel ${change.changeNumber}?`,
      message: 'Cancelling closes the change and returns its revisions to draft.',
      label: 'Reason (optional)',
      confirmText: 'Cancel change',
      required: false,
      danger: true
    }).subscribe(comment => this.run(this.api.cancel(change.id, comment || null), `${change.changeNumber} cancelled.`));
  }

  protected itemLink(item: AffectedItem): (string | number)[] {
    return [item.itemType === 'Product' ? '/products' : '/components', item.itemId];
  }

  private activeStepName(): string {
    return this.change()?.approvalSteps.find(s => s.status === 'Active')?.name ?? '';
  }

  private load(id: number): void {
    this.api.get(id).subscribe(change => {
      this.change.set(change);
      this.loadPlannedWorkflow(change);
    });
    this.loadHistory(id);
  }

  private loadHistory(id: number): void {
    this.api.history(id).subscribe(entries => this.history.set(entries));
  }

  /** Draft changes have no step snapshot yet, so preview the selected workflow's steps instead. */
  private loadPlannedWorkflow(change: ChangeDetail): void {
    if (change.approvalSteps.length > 0) {
      this.plannedWorkflow.set(null);
      return;
    }

    this.workflowsApi.get(change.workflowDefinitionId).subscribe(workflow => this.plannedWorkflow.set(workflow));
  }

  private run(request: Observable<ChangeDetail>, message: string): void {
    this.busy.set(true);
    request.subscribe({
      next: updated => this.apply(updated, message),
      error: () => this.busy.set(false)
    });
  }

  private apply(updated: ChangeDetail, message: string): void {
    this.busy.set(false);
    this.change.set(updated);
    this.loadPlannedWorkflow(updated);
    this.loadHistory(updated.id);
    this.notify.success(message);
  }
}

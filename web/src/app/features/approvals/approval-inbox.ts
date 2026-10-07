import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { ApprovalsApi, ChangesApi } from '../../core/api/changes.api';
import { ChangeDetail, PendingApproval } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { openComment } from '../../shared/dialogs';
import { StatusChip } from '../../shared/status-chip';

@Component({
  selector: 'app-approval-inbox',
  imports: [DatePipe, RouterLink, MatButtonModule, MatIconModule, MatProgressBarModule, MatTableModule, StatusChip],
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <h1>My approvals</h1>
          <p class="subtitle">Engineering changes waiting at a step you can sign off. Your own requests never appear here.</p>
        </div>
        <button mat-stroked-button (click)="load()"><mat-icon>refresh</mat-icon> Refresh</button>
      </header>

      <div class="table-container">
        @if (loading()) {
          <mat-progress-bar mode="indeterminate" />
        }
        <table mat-table class="data-table" [dataSource]="items()">
          <ng-container matColumnDef="changeNumber">
            <th mat-header-cell *matHeaderCellDef>Change</th>
            <td mat-cell *matCellDef="let item">
              <a class="number-link" [routerLink]="['/changes', item.changeId]">{{ item.changeNumber }}</a>
            </td>
          </ng-container>
          <ng-container matColumnDef="title">
            <th mat-header-cell *matHeaderCellDef>Title</th>
            <td mat-cell *matCellDef="let item">
              {{ item.title }}
              <div class="muted">Requested by {{ item.requestedBy }}</div>
            </td>
          </ng-container>
          <ng-container matColumnDef="priority">
            <th mat-header-cell *matHeaderCellDef>Priority</th>
            <td mat-cell *matCellDef="let item"><app-status-chip [value]="item.priority" /></td>
          </ng-container>
          <ng-container matColumnDef="step">
            <th mat-header-cell *matHeaderCellDef>Step</th>
            <td mat-cell *matCellDef="let item">
              {{ item.stepName }}
              <div class="muted">
                Step {{ item.stepOrder }} of {{ item.totalSteps }} · {{ item.approvalsReceived }}/{{ item.requiredApprovals }} approvals
              </div>
            </td>
          </ng-container>
          <ng-container matColumnDef="waiting">
            <th mat-header-cell *matHeaderCellDef>Waiting since</th>
            <td mat-cell *matCellDef="let item">{{ item.stepActivatedAtUtc | date: 'medium' }}</td>
          </ng-container>
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let item" class="actions-cell">
              <button mat-stroked-button [disabled]="busy()" (click)="reject(item)">Reject</button>
              <button mat-flat-button [disabled]="busy()" (click)="approve(item)">Approve</button>
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
          <tr class="mat-mdc-row" *matNoDataRow>
            <td class="mat-mdc-cell empty" [attr.colspan]="columns.length">
              {{ loading() ? 'Loading…' : 'Nothing is waiting for your approval.' }}
            </td>
          </tr>
        </table>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ApprovalInboxPage {
  private readonly approvalsApi = inject(ApprovalsApi);
  private readonly changesApi = inject(ChangesApi);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);

  protected readonly columns = ['changeNumber', 'title', 'priority', 'step', 'waiting', 'actions'];
  protected readonly items = signal<PendingApproval[]>([]);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.approvalsApi.pending().subscribe({
      next: items => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  protected approve(item: PendingApproval): void {
    openComment(this.dialog, {
      title: `Approve ${item.changeNumber}`,
      message: `Step "${item.stepName}" · ${item.title}`,
      label: 'Comment (optional)',
      confirmText: 'Approve',
      required: false
    }).subscribe(comment => this.decide(this.changesApi.approve(item.changeId, comment || null), `${item.changeNumber} approved.`));
  }

  protected reject(item: PendingApproval): void {
    openComment(this.dialog, {
      title: `Reject ${item.changeNumber}`,
      message: 'Rejecting closes the change and returns its revisions to draft.',
      label: 'Reason for rejection',
      confirmText: 'Reject',
      required: true,
      danger: true
    }).subscribe(comment => this.decide(this.changesApi.reject(item.changeId, comment), `${item.changeNumber} rejected.`));
  }

  private decide(request: Observable<ChangeDetail>, message: string): void {
    this.busy.set(true);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        this.notify.success(message);
        this.load();
      },
      error: () => this.busy.set(false)
    });
  }
}

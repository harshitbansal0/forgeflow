import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { filter } from 'rxjs';
import { WorkflowsApi } from '../../core/api/admin.api';
import { Workflow } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { WorkflowEditorDialog } from './workflow-editor-dialog';

@Component({
  selector: 'app-workflow-list',
  imports: [DatePipe, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <h1>Approval workflows</h1>
          <p class="subtitle">
            Configure the sign-off steps engineering changes go through. Changes in review keep the steps they were submitted with.
          </p>
        </div>
        <button mat-flat-button (click)="edit(null)"><mat-icon>add</mat-icon> New workflow</button>
      </header>

      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }

      <div class="panel-grid">
        @for (workflow of workflows(); track workflow.id) {
          <section class="panel">
            <div class="panel-title">
              <h2>{{ workflow.name }}</h2>
              <button mat-icon-button aria-label="Edit workflow" (click)="edit(workflow)"><mat-icon>edit</mat-icon></button>
            </div>
            <div class="header-meta">
              @if (workflow.isDefault) {
                <span class="status-chip" data-tone="info">Default</span>
              }
              <span class="status-chip" [attr.data-tone]="workflow.isActive ? 'success' : 'muted'">
                {{ workflow.isActive ? 'Active' : 'Inactive' }}
              </span>
              <span class="muted">Used by {{ workflow.usageCount }} change{{ workflow.usageCount === 1 ? '' : 's' }}</span>
            </div>
            @if (workflow.description) {
              <p>{{ workflow.description }}</p>
            }
            <ol class="step-list">
              @for (step of workflow.steps; track step.id) {
                <li class="step-item">
                  <span class="step-index">{{ step.stepOrder }}</span>
                  <div>
                    <div class="step-title">{{ step.name }}</div>
                    <div class="step-meta">
                      {{ step.approverRole }} · {{ step.requiredApprovals }} approval{{ step.requiredApprovals === 1 ? '' : 's' }}
                    </div>
                  </div>
                </li>
              }
            </ol>
            <p class="muted">Updated {{ (workflow.updatedAtUtc ?? workflow.createdAtUtc) | date: 'mediumDate' }}</p>
          </section>
        }
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WorkflowListPage {
  private readonly api = inject(WorkflowsApi);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);

  protected readonly workflows = signal<Workflow[]>([]);
  protected readonly loading = signal(true);

  constructor() {
    this.load();
  }

  protected edit(workflow: Workflow | null): void {
    this.dialog
      .open<WorkflowEditorDialog, Workflow | null, Workflow>(WorkflowEditorDialog, { data: workflow, width: '760px' })
      .afterClosed()
      .pipe(filter((saved): saved is Workflow => !!saved))
      .subscribe(saved => {
        this.notify.success(`Workflow "${saved.name}" saved.`);
        this.load();
      });
  }

  private load(): void {
    this.loading.set(true);
    this.api.list().subscribe({
      next: workflows => {
        this.workflows.set(workflows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}

import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { WorkflowsApi } from '../../core/api/admin.api';
import { ChangesApi } from '../../core/api/changes.api';
import { CHANGE_PRIORITIES, ChangeDetail, ChangePriority } from '../../core/models';

@Component({
  selector: 'app-change-edit-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Edit {{ change.changeNumber }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-grid" id="change-edit-form" (ngSubmit)="save()">
        <mat-form-field class="full-width">
          <mat-label>Title</mat-label>
          <input matInput formControlName="title" />
        </mat-form-field>
        <mat-form-field class="full-width">
          <mat-label>Description</mat-label>
          <textarea matInput rows="4" formControlName="description"></textarea>
        </mat-form-field>
        <mat-form-field class="full-width">
          <mat-label>Reason / justification</mat-label>
          <textarea matInput rows="2" formControlName="reason"></textarea>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Priority</mat-label>
          <mat-select formControlName="priority">
            @for (priority of priorities; track priority) {
              <mat-option [value]="priority">{{ priority }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Approval workflow</mat-label>
          <mat-select formControlName="workflowDefinitionId">
            @for (workflow of workflows(); track workflow.id) {
              <mat-option [value]="workflow.id">{{ workflow.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button type="submit" form="change-edit-form" [disabled]="saving()">Save changes</button>
    </mat-dialog-actions>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangeEditDialog {
  protected readonly change = inject<ChangeDetail>(MAT_DIALOG_DATA);
  private readonly api = inject(ChangesApi);
  private readonly dialogRef = inject<MatDialogRef<ChangeEditDialog, ChangeDetail>>(MatDialogRef);
  protected readonly priorities = CHANGE_PRIORITIES;
  protected readonly workflows = toSignal(inject(WorkflowsApi).list(true), { initialValue: [] });
  protected readonly saving = signal(false);

  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly form = this.fb.group({
    title: [this.change.title, [Validators.required, Validators.maxLength(200)]],
    description: [this.change.description, [Validators.required, Validators.maxLength(4000)]],
    reason: [this.change.reason ?? '', Validators.maxLength(2000)],
    priority: this.fb.control<ChangePriority>(this.change.priority),
    workflowDefinitionId: this.fb.control<number>(this.change.workflowDefinitionId, Validators.required)
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.api
      .update(this.change.id, {
        title: value.title.trim(),
        description: value.description.trim(),
        reason: value.reason.trim() || null,
        priority: value.priority,
        workflowDefinitionId: value.workflowDefinitionId
      })
      .subscribe({
        next: change => this.dialogRef.close(change),
        error: () => this.saving.set(false)
      });
  }
}

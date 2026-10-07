import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { WorkflowsApi } from '../../core/api/admin.api';
import { APPROVER_ROLES, UserRole, Workflow } from '../../core/models';

type StepForm = FormGroup<{
  name: FormControl<string>;
  approverRole: FormControl<UserRole>;
  requiredApprovals: FormControl<number>;
}>;

@Component({
  selector: 'app-workflow-editor-dialog',
  imports: [
    ReactiveFormsModule,
    DragDropModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule
  ],
  template: `
    <h2 mat-dialog-title>{{ workflow ? 'Edit workflow' : 'New approval workflow' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" id="workflow-form" (ngSubmit)="save()">
        <div class="form-grid">
          <mat-form-field class="full-width">
            <mat-label>Name</mat-label>
            <input matInput formControlName="name" />
            @if (form.controls.name.invalid) {
              <mat-error>A name is required (max 120 characters).</mat-error>
            }
          </mat-form-field>
          <mat-form-field class="full-width">
            <mat-label>Description</mat-label>
            <textarea matInput rows="2" formControlName="description"></textarea>
          </mat-form-field>
          <mat-slide-toggle formControlName="isActive">Active</mat-slide-toggle>
          <mat-slide-toggle formControlName="isDefault">Default for new changes</mat-slide-toggle>
        </div>

        <div class="panel-title" style="margin-top: 16px">
          <h3>Approval steps</h3>
          <button mat-stroked-button type="button" [disabled]="steps.length >= 10" (click)="addStep()">
            <mat-icon>add</mat-icon> Add step
          </button>
        </div>
        <p class="muted">Steps run in order. Drag to reorder. A step completes when it has the required number of approvals.</p>

        <div cdkDropList (cdkDropListDropped)="reorder($event)" formArrayName="steps">
          @for (step of steps.controls; track step; let i = $index) {
            <div class="workflow-step-row" cdkDrag [formGroupName]="i">
              <mat-icon cdkDragHandle class="drag-handle" matTooltip="Drag to reorder">drag_indicator</mat-icon>
              <span class="step-number">{{ i + 1 }}</span>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>Step name</mat-label>
                <input matInput formControlName="name" />
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>Approver role</mat-label>
                <mat-select formControlName="approverRole">
                  @for (role of roles; track role) {
                    <mat-option [value]="role">{{ role }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
              <mat-form-field subscriptSizing="dynamic">
                <mat-label>Approvals</mat-label>
                <input matInput type="number" min="1" max="5" formControlName="requiredApprovals" />
              </mat-form-field>
              <button mat-icon-button type="button" aria-label="Remove step" [disabled]="steps.length === 1" (click)="removeStep(i)">
                <mat-icon>delete_outline</mat-icon>
              </button>
            </div>
          }
        </div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button type="submit" form="workflow-form" [disabled]="saving()">Save workflow</button>
    </mat-dialog-actions>
  `,
  styles: `
    .workflow-step-row {
      display: grid;
      grid-template-columns: 24px 24px 1fr 160px 110px 40px;
      gap: 8px;
      align-items: center;
      padding: 8px 0;
      background: var(--mat-sys-surface);
    }
    .drag-handle {
      cursor: move;
      color: var(--mat-sys-on-surface-variant);
    }
    .step-number {
      font-weight: 500;
      text-align: center;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WorkflowEditorDialog {
  protected readonly workflow = inject<Workflow | null>(MAT_DIALOG_DATA);
  private readonly api = inject(WorkflowsApi);
  private readonly dialogRef = inject<MatDialogRef<WorkflowEditorDialog, Workflow>>(MatDialogRef);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly roles = APPROVER_ROLES;
  protected readonly saving = signal(false);
  protected readonly form = this.fb.group({
    name: [this.workflow?.name ?? '', [Validators.required, Validators.maxLength(120)]],
    description: [this.workflow?.description ?? '', Validators.maxLength(1000)],
    isActive: [this.workflow?.isActive ?? true],
    isDefault: [this.workflow?.isDefault ?? false],
    steps: this.fb.array<StepForm>(
      this.workflow
        ? this.workflow.steps.map(step => this.createStep(step.name, step.approverRole, step.requiredApprovals))
        : [this.createStep('Technical Review', 'Approver', 1)]
    )
  });

  protected get steps(): FormArray<StepForm> {
    return this.form.controls.steps;
  }

  protected addStep(): void {
    this.steps.push(this.createStep('', 'Approver', 1));
  }

  protected removeStep(index: number): void {
    this.steps.removeAt(index);
  }

  protected reorder(event: CdkDragDrop<unknown>): void {
    const controls = [...this.steps.controls];
    moveItemInArray(controls, event.previousIndex, event.currentIndex);
    this.steps.clear();
    controls.forEach(control => this.steps.push(control));
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const input = {
      name: value.name.trim(),
      description: value.description.trim() || null,
      isActive: value.isActive,
      isDefault: value.isDefault,
      steps: value.steps.map(step => ({ ...step, name: step.name.trim(), requiredApprovals: Number(step.requiredApprovals) }))
    };

    this.saving.set(true);
    const request = this.workflow ? this.api.update(this.workflow.id, input) : this.api.create(input);
    request.subscribe({
      next: workflow => this.dialogRef.close(workflow),
      error: () => this.saving.set(false)
    });
  }

  private createStep(name: string, approverRole: UserRole, requiredApprovals: number): StepForm {
    return this.fb.group({
      name: this.fb.control(name, [Validators.required, Validators.maxLength(120)]),
      approverRole: this.fb.control(approverRole),
      requiredApprovals: this.fb.control(requiredApprovals, [Validators.required, Validators.min(1), Validators.max(5)])
    });
  }
}

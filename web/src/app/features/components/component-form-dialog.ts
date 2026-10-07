import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ComponentsApi } from '../../core/api/components.api';
import {
  COMPONENT_TYPES,
  ComponentDetail,
  ComponentInput,
  ComponentType,
  ITEM_NUMBER_PATTERN,
  LIFECYCLE_STATES,
  LifecycleState
} from '../../core/models';
import { EnumLabelPipe } from '../../shared/status-chip';

export interface ComponentFormData {
  component?: ComponentDetail;
}

@Component({
  selector: 'app-component-form-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, EnumLabelPipe],
  template: `
    <h2 mat-dialog-title>{{ component ? 'Edit ' + component.partNumber : 'New component' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-grid" id="component-form" (ngSubmit)="save()">
        <mat-form-field>
          <mat-label>Part number</mat-label>
          <input matInput formControlName="partNumber" placeholder="CMP-1001" />
          @if (form.controls.partNumber.invalid) {
            <mat-error>Use 3-40 letters, digits or hyphens.</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>Type</mat-label>
          <mat-select formControlName="type">
            @for (type of componentTypes; track type) {
              <mat-option [value]="type">{{ type }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field class="full-width">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" />
          @if (form.controls.name.invalid) {
            <mat-error>Name is required (max 200 characters).</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>Material</mat-label>
          <input matInput formControlName="material" />
        </mat-form-field>
        <mat-form-field>
          <mat-label>Supplier</mat-label>
          <input matInput formControlName="supplier" />
        </mat-form-field>
        <mat-form-field>
          <mat-label>Unit of measure</mat-label>
          <input matInput formControlName="unitOfMeasure" placeholder="EA" />
          @if (form.controls.unitOfMeasure.invalid) {
            <mat-error>Required, up to 10 characters.</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>Unit cost (EUR)</mat-label>
          <input matInput type="number" min="0" step="0.01" formControlName="unitCost" />
        </mat-form-field>
        <mat-form-field>
          <mat-label>Lifecycle state</mat-label>
          <mat-select formControlName="lifecycleState">
            @for (state of lifecycleStates; track state) {
              <mat-option [value]="state">{{ state | enumLabel }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (!component) {
          <mat-form-field>
            <mat-label>Drawing number (Rev A)</mat-label>
            <input matInput formControlName="drawingNumber" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>Weight (kg, Rev A)</mat-label>
            <input matInput type="number" min="0" step="0.001" formControlName="weightKg" />
          </mat-form-field>
        }
        <mat-form-field class="full-width">
          <mat-label>Description</mat-label>
          <textarea matInput rows="3" formControlName="description"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button type="submit" form="component-form" [disabled]="saving()">
        {{ component ? 'Save changes' : 'Create component' }}
      </button>
    </mat-dialog-actions>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ComponentFormDialog {
  private readonly api = inject(ComponentsApi);
  private readonly dialogRef = inject<MatDialogRef<ComponentFormDialog, ComponentDetail>>(MatDialogRef);
  protected readonly component = inject<ComponentFormData>(MAT_DIALOG_DATA).component;
  protected readonly componentTypes = COMPONENT_TYPES;
  protected readonly lifecycleStates = LIFECYCLE_STATES;
  protected readonly saving = signal(false);

  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly form = this.fb.group({
    partNumber: this.fb.control({ value: this.component?.partNumber ?? '', disabled: !!this.component }, [
      Validators.required,
      Validators.pattern(ITEM_NUMBER_PATTERN)
    ]),
    name: [this.component?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    type: this.fb.control<ComponentType>(this.component?.type ?? 'Mechanical'),
    material: [this.component?.material ?? '', Validators.maxLength(100)],
    supplier: [this.component?.supplier ?? '', Validators.maxLength(200)],
    unitOfMeasure: [this.component?.unitOfMeasure ?? 'EA', [Validators.required, Validators.maxLength(10)]],
    unitCost: this.fb.control<number | null>(this.component?.unitCost ?? null, Validators.min(0)),
    lifecycleState: this.fb.control<LifecycleState>(this.component?.lifecycleState ?? 'Concept'),
    drawingNumber: ['', Validators.maxLength(60)],
    weightKg: this.fb.control<number | null>(null, Validators.min(0)),
    description: [this.component?.description ?? '', Validators.maxLength(2000)]
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const input: ComponentInput = {
      partNumber: value.partNumber,
      name: value.name,
      description: value.description || null,
      type: value.type,
      material: value.material || null,
      unitOfMeasure: value.unitOfMeasure,
      supplier: value.supplier || null,
      unitCost: value.unitCost,
      lifecycleState: value.lifecycleState,
      drawingNumber: value.drawingNumber || null,
      weightKg: value.weightKg
    };

    this.saving.set(true);
    const request = this.component ? this.api.update(this.component.id, input) : this.api.create(input);
    request.subscribe({
      next: component => this.dialogRef.close(component),
      error: () => this.saving.set(false)
    });
  }
}

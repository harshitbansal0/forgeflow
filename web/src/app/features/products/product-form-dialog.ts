import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { UsersApi } from '../../core/api/admin.api';
import { ProductsApi } from '../../core/api/products.api';
import { ITEM_NUMBER_PATTERN, LIFECYCLE_STATES, LifecycleState, ProductDetail } from '../../core/models';
import { EnumLabelPipe } from '../../shared/status-chip';

export interface ProductFormData {
  product?: ProductDetail;
}

@Component({
  selector: 'app-product-form-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatAutocompleteModule,
    MatButtonModule,
    EnumLabelPipe
  ],
  template: `
    <h2 mat-dialog-title>{{ product ? 'Edit ' + product.productNumber : 'New product' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-grid" id="product-form" (ngSubmit)="save()">
        <mat-form-field>
          <mat-label>Product number</mat-label>
          <input matInput formControlName="productNumber" placeholder="FF-ACT-100" />
          <mat-hint>Letters, digits and hyphens. Saved in upper case.</mat-hint>
          @if (form.controls.productNumber.invalid) {
            <mat-error>Use 3-40 letters, digits or hyphens.</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>Lifecycle state</mat-label>
          <mat-select formControlName="lifecycleState">
            @for (state of lifecycleStates; track state) {
              <mat-option [value]="state">{{ state | enumLabel }}</mat-option>
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
          <mat-label>Category</mat-label>
          <input matInput formControlName="category" [matAutocomplete]="categoryPanel" />
          <mat-autocomplete #categoryPanel="matAutocomplete">
            @for (category of categories(); track category) {
              <mat-option [value]="category">{{ category }}</mat-option>
            }
          </mat-autocomplete>
          @if (form.controls.category.invalid) {
            <mat-error>Category is required.</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>Owner</mat-label>
          <mat-select formControlName="ownerId">
            @if (!product) {
              <mat-option [value]="null">Me</mat-option>
            }
            @for (user of owners(); track user.id) {
              <mat-option [value]="user.id">{{ user.displayName }} ({{ user.role }})</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <mat-form-field class="full-width">
          <mat-label>Description</mat-label>
          <textarea matInput rows="3" formControlName="description"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button type="submit" form="product-form" [disabled]="saving()">
        {{ product ? 'Save changes' : 'Create product' }}
      </button>
    </mat-dialog-actions>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductFormDialog {
  private readonly api = inject(ProductsApi);
  private readonly dialogRef = inject<MatDialogRef<ProductFormDialog, ProductDetail>>(MatDialogRef);
  protected readonly product = inject<ProductFormData>(MAT_DIALOG_DATA).product;
  protected readonly lifecycleStates = LIFECYCLE_STATES;
  protected readonly owners = toSignal(inject(UsersApi).directory(), { initialValue: [] });
  protected readonly categories = toSignal(this.api.categories(), { initialValue: [] });
  protected readonly saving = signal(false);

  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly form = this.fb.group({
    productNumber: this.fb.control({ value: this.product?.productNumber ?? '', disabled: !!this.product }, [
      Validators.required,
      Validators.pattern(ITEM_NUMBER_PATTERN)
    ]),
    name: [this.product?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    category: [this.product?.category ?? '', [Validators.required, Validators.maxLength(100)]],
    lifecycleState: this.fb.control<LifecycleState>(this.product?.lifecycleState ?? 'Concept'),
    ownerId: this.fb.control<number | null>(this.product?.ownerId ?? null),
    description: [this.product?.description ?? '', Validators.maxLength(2000)]
  });

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const input = {
      productNumber: value.productNumber,
      name: value.name,
      category: value.category,
      lifecycleState: value.lifecycleState,
      ownerId: value.ownerId,
      description: value.description || null
    };

    this.saving.set(true);
    const request = this.product ? this.api.update(this.product.id, input) : this.api.create(input);
    request.subscribe({
      next: product => this.dialogRef.close(product),
      error: () => this.saving.set(false)
    });
  }
}

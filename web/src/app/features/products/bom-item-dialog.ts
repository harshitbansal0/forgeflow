import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { catchError, debounceTime, distinctUntilChanged, filter, map, of, startWith, switchMap } from 'rxjs';
import { ComponentsApi } from '../../core/api/components.api';
import { BomItem, BomItemInput, ComponentSummary } from '../../core/models';

export interface BomItemDialogData {
  item?: BomItem;
  excludedComponentIds: number[];
}

@Component({
  selector: 'app-bom-item-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.item ? 'Edit BOM line ' + data.item.partNumber : 'Add component to BOM' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form-grid" id="bom-form" (ngSubmit)="save()">
        @if (!data.item) {
          <mat-form-field class="full-width">
            <mat-label>Component</mat-label>
            <input matInput [formControl]="componentSearch" [matAutocomplete]="componentPanel" placeholder="Search part number or name" />
            <mat-autocomplete #componentPanel="matAutocomplete" [displayWith]="displayComponent" (optionSelected)="selected.set($event.option.value)">
              @for (component of options(); track component.id) {
                <mat-option [value]="component">
                  <strong>{{ component.partNumber }}</strong> {{ component.name }} · {{ component.unitOfMeasure }}
                </mat-option>
              }
            </mat-autocomplete>
            @if (!selected()) {
              <mat-hint>Select a component from the list.</mat-hint>
            }
          </mat-form-field>
        }
        <mat-form-field>
          <mat-label>Quantity{{ unit() ? ' (' + unit() + ')' : '' }}</mat-label>
          <input matInput type="number" min="0.0001" step="any" formControlName="quantity" />
          @if (form.controls.quantity.invalid) {
            <mat-error>Enter a quantity greater than zero.</mat-error>
          }
        </mat-form-field>
        <mat-form-field>
          <mat-label>Reference designator</mat-label>
          <input matInput formControlName="referenceDesignator" placeholder="e.g. B1-B4" />
        </mat-form-field>
        <mat-form-field class="full-width">
          <mat-label>Notes</mat-label>
          <textarea matInput rows="2" formControlName="notes"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button type="submit" form="bom-form" [disabled]="!data.item && !selected()">
        {{ data.item ? 'Save line' : 'Add to BOM' }}
      </button>
    </mat-dialog-actions>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BomItemDialog {
  protected readonly data = inject<BomItemDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<BomItemDialog, BomItemInput>>(MatDialogRef);
  private readonly componentsApi = inject(ComponentsApi);

  protected readonly selected = signal<ComponentSummary | null>(null);
  protected readonly componentSearch = new FormControl<string | ComponentSummary>('', { nonNullable: true });
  protected readonly options = toSignal(
    this.componentSearch.valueChanges.pipe(
      startWith(''),
      filter((value): value is string => typeof value === 'string'),
      debounceTime(250),
      distinctUntilChanged(),
      switchMap(term =>
        this.componentsApi.list({ search: term.trim(), pageSize: 15, sortBy: 'partNumber', sortDirection: 'asc' }).pipe(
          map(page => page.items.filter(c => c.lifecycleState !== 'Obsolete' && !this.data.excludedComponentIds.includes(c.id))),
          catchError(() => of<ComponentSummary[]>([]))
        )
      )
    ),
    { initialValue: [] }
  );

  protected readonly form = inject(NonNullableFormBuilder).group({
    quantity: [this.data.item?.quantity ?? 1, [Validators.required, Validators.min(0.0001)]],
    referenceDesignator: [this.data.item?.referenceDesignator ?? '', Validators.maxLength(100)],
    notes: [this.data.item?.notes ?? '', Validators.maxLength(500)]
  });

  protected unit(): string {
    return this.data.item?.unitOfMeasure ?? this.selected()?.unitOfMeasure ?? '';
  }

  protected readonly displayComponent = (component: ComponentSummary | string | null): string =>
    typeof component === 'string' ? component : component ? `${component.partNumber} ${component.name}` : '';

  protected save(): void {
    if (this.form.invalid || (!this.data.item && !this.selected())) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.dialogRef.close({
      componentId: this.selected()?.id,
      quantity: Number(value.quantity),
      referenceDesignator: value.referenceDesignator.trim() || null,
      notes: value.notes.trim() || null
    });
  }
}

import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Observable, catchError, combineLatest, debounceTime, distinctUntilChanged, filter, map, of, startWith, switchMap } from 'rxjs';
import { ComponentsApi } from '../../core/api/components.api';
import { ProductsApi } from '../../core/api/products.api';
import { AffectedItemType, LifecycleState } from '../../core/models';
import { StatusChip } from '../../shared/status-chip';

export interface PickedItem {
  itemType: AffectedItemType;
  itemId: number;
  number: string;
  name: string;
  note: string | null;
}

export interface ItemPickerData {
  excluded: { itemType: AffectedItemType; itemId: number }[];
}

interface ItemOption {
  id: number;
  number: string;
  name: string;
  lifecycleState: LifecycleState;
  workingRevision: string | null;
  releasedRevision: string | null;
}

@Component({
  selector: 'app-item-picker-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatInputModule,
    MatAutocompleteModule,
    StatusChip
  ],
  template: `
    <h2 mat-dialog-title>Add affected item</h2>
    <mat-dialog-content>
      <p class="muted">
        The item's working draft revision is used. If it has none, the next revision is created automatically.
      </p>
      <mat-button-toggle-group [value]="itemType()" (change)="setType($event.value)" aria-label="Item type">
        <mat-button-toggle value="Product">Product</mat-button-toggle>
        <mat-button-toggle value="Component">Component</mat-button-toggle>
      </mat-button-toggle-group>
      <mat-form-field class="full-width" style="margin-top: 16px">
        <mat-label>{{ itemType() }}</mat-label>
        <input matInput [formControl]="search" [matAutocomplete]="panel" placeholder="Search number or name" />
        <mat-autocomplete #panel="matAutocomplete" [displayWith]="display" (optionSelected)="selected.set($event.option.value)">
          @for (option of options(); track option.id) {
            <mat-option [value]="option">
              <strong>{{ option.number }}</strong> {{ option.name }}
              <app-status-chip [value]="option.lifecycleState" />
            </mat-option>
          }
        </mat-autocomplete>
      </mat-form-field>
      <mat-form-field class="full-width">
        <mat-label>What changes on this item?</mat-label>
        <textarea matInput rows="2" [formControl]="note"></textarea>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button [disabled]="!selected()" (click)="confirm()">Add item</button>
    </mat-dialog-actions>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ItemPickerDialog {
  private readonly data = inject<ItemPickerData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<ItemPickerDialog, PickedItem>>(MatDialogRef);
  private readonly productsApi = inject(ProductsApi);
  private readonly componentsApi = inject(ComponentsApi);

  protected readonly itemType = signal<AffectedItemType>('Product');
  protected readonly selected = signal<ItemOption | null>(null);
  protected readonly search = new FormControl<string | ItemOption>('', { nonNullable: true });
  protected readonly note = new FormControl('', { nonNullable: true, validators: Validators.maxLength(500) });

  protected readonly options = toSignal(
    combineLatest([
      toObservable(this.itemType),
      this.search.valueChanges.pipe(
        startWith(''),
        filter((value): value is string => typeof value === 'string'),
        debounceTime(250),
        distinctUntilChanged()
      )
    ]).pipe(switchMap(([type, term]) => this.find(type, term.trim()))),
    { initialValue: [] }
  );

  protected readonly display = (option: ItemOption | string | null): string =>
    typeof option === 'string' ? option : option ? `${option.number} ${option.name}` : '';

  protected setType(type: AffectedItemType): void {
    this.itemType.set(type);
    this.selected.set(null);
    this.search.setValue('');
  }

  protected confirm(): void {
    const option = this.selected();
    if (option) {
      this.dialogRef.close({
        itemType: this.itemType(),
        itemId: option.id,
        number: option.number,
        name: option.name,
        note: this.note.value.trim() || null
      });
    }
  }

  private find(type: AffectedItemType, term: string): Observable<ItemOption[]> {
    const query = { search: term, pageSize: 15, sortDirection: 'asc' };
    const results: Observable<ItemOption[]> =
      type === 'Product'
        ? this.productsApi.list({ ...query, sortBy: 'productNumber' }).pipe(
            map(page => page.items.map(p => ({ ...p, number: p.productNumber })))
          )
        : this.componentsApi.list({ ...query, sortBy: 'partNumber' }).pipe(
            map(page => page.items.map(c => ({ ...c, number: c.partNumber })))
          );

    return results.pipe(
      map(items =>
        items.filter(
          item =>
            item.lifecycleState !== 'Obsolete' &&
            !this.data.excluded.some(e => e.itemType === type && e.itemId === item.id)
        )
      ),
      catchError(() => of([]))
    );
  }
}

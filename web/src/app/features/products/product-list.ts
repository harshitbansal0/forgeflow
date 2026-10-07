import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { ProductsApi } from '../../core/api/products.api';
import { AuthService } from '../../core/auth/auth.service';
import { LIFECYCLE_STATES, ProductDetail, ProductSummary } from '../../core/models';
import { PagedList } from '../../shared/paged-list';
import { EnumLabelPipe, StatusChip } from '../../shared/status-chip';
import { ProductFormData, ProductFormDialog } from './product-form-dialog';

@Component({
  selector: 'app-product-list',
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatButtonModule,
    MatProgressBarModule,
    EnumLabelPipe,
    StatusChip
  ],
  templateUrl: './product-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProductListPage {
  private readonly api = inject(ProductsApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  protected readonly columns = ['productNumber', 'name', 'category', 'lifecycleState', 'revision', 'owner', 'updatedAt'];
  protected readonly lifecycleStates = LIFECYCLE_STATES;
  protected readonly categories = toSignal(this.api.categories(), { initialValue: [] });
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly list = new PagedList<ProductSummary>(query => this.api.list(query), { active: 'updatedAt', direction: 'desc' });

  constructor() {
    this.list.connectSearch(this.search);
  }

  protected open(product: ProductSummary): void {
    void this.router.navigate(['/products', product.id]);
  }

  protected create(): void {
    this.dialog
      .open<ProductFormDialog, ProductFormData, ProductDetail>(ProductFormDialog, { data: {}, width: '640px' })
      .afterClosed()
      .subscribe(product => {
        if (product) {
          void this.router.navigate(['/products', product.id]);
        }
      });
  }
}

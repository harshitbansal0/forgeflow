import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
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
import { ComponentsApi } from '../../core/api/components.api';
import { AuthService } from '../../core/auth/auth.service';
import { COMPONENT_TYPES, ComponentDetail, ComponentSummary, LIFECYCLE_STATES } from '../../core/models';
import { PagedList } from '../../shared/paged-list';
import { EnumLabelPipe, StatusChip } from '../../shared/status-chip';
import { ComponentFormData, ComponentFormDialog } from './component-form-dialog';

@Component({
  selector: 'app-component-list',
  imports: [
    CurrencyPipe,
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
  templateUrl: './component-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ComponentListPage {
  private readonly api = inject(ComponentsApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  protected readonly columns = ['partNumber', 'name', 'type', 'lifecycleState', 'revision', 'supplier', 'unitCost', 'updatedAt'];
  protected readonly componentTypes = COMPONENT_TYPES;
  protected readonly lifecycleStates = LIFECYCLE_STATES;
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly list = new PagedList<ComponentSummary>(query => this.api.list(query), { active: 'updatedAt', direction: 'desc' });

  constructor() {
    this.list.connectSearch(this.search);
  }

  protected open(component: ComponentSummary): void {
    void this.router.navigate(['/components', component.id]);
  }

  protected create(): void {
    this.dialog
      .open<ComponentFormDialog, ComponentFormData, ComponentDetail>(ComponentFormDialog, { data: {}, width: '680px' })
      .afterClosed()
      .subscribe(component => {
        if (component) {
          void this.router.navigate(['/components', component.id]);
        }
      });
  }
}

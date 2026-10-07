import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, filter, map, of, startWith, switchMap } from 'rxjs';
import { SearchApi } from '../core/api/admin.api';
import { ApprovalsApi } from '../core/api/changes.api';
import { AuthService } from '../core/auth/auth.service';
import { SearchResult, UserRole } from '../core/models';
import { EnumLabelPipe } from '../shared/status-chip';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  roles?: UserRole[];
}

const NAV_ITEMS: NavItem[] = [
  { path: '/dashboard', label: 'Dashboard', icon: 'space_dashboard' },
  { path: '/products', label: 'Products', icon: 'inventory_2' },
  { path: '/components', label: 'Components', icon: 'settings_input_component' },
  { path: '/changes', label: 'Engineering changes', icon: 'published_with_changes' },
  { path: '/approvals', label: 'My approvals', icon: 'fact_check', roles: ['Engineer', 'Approver', 'Admin'] },
  { path: '/workflows', label: 'Approval workflows', icon: 'account_tree', roles: ['Admin'] },
  { path: '/audit', label: 'Audit log', icon: 'policy', roles: ['Approver', 'Admin'] },
  { path: '/users', label: 'Users', icon: 'group', roles: ['Admin'] }
];

const RESULT_ROUTES: Record<SearchResult['type'], string> = {
  Product: 'products',
  Component: 'components',
  EngineeringChange: 'changes'
};

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    ReactiveFormsModule,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatAutocompleteModule,
    EnumLabelPipe
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Shell {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly approvalsApi = inject(ApprovalsApi);
  private readonly searchApi = inject(SearchApi);

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver).observe([Breakpoints.XSmall, Breakpoints.Small]).pipe(map(state => state.matches)),
    { initialValue: false }
  );
  protected readonly navItems = computed(() => NAV_ITEMS.filter(item => !item.roles || this.auth.hasRole(...item.roles)));
  protected readonly pendingApprovals = signal(0);
  protected readonly searchControl = new FormControl<string | SearchResult>('', { nonNullable: true });
  protected readonly searchResults = toSignal(
    this.searchControl.valueChanges.pipe(
      filter((value): value is string => typeof value === 'string'),
      debounceTime(250),
      map(value => value.trim()),
      distinctUntilChanged(),
      switchMap(term => (term.length < 2 ? of<SearchResult[]>([]) : this.searchApi.search(term).pipe(catchError(() => of([])))))
    ),
    { initialValue: [] }
  );

  constructor() {
    this.router.events
      .pipe(
        filter(event => event instanceof NavigationEnd),
        startWith(null),
        takeUntilDestroyed()
      )
      .subscribe(() => this.refreshPendingApprovals());
  }

  protected readonly displayResult = (result: SearchResult | string | null): string =>
    typeof result === 'string' ? result : result ? `${result.number} ${result.title}` : '';

  protected openResult(result: SearchResult): void {
    this.searchControl.setValue('', { emitEvent: false });
    void this.router.navigate(['/', RESULT_ROUTES[result.type], result.id]);
  }

  private refreshPendingApprovals(): void {
    if (!this.auth.canApprove()) {
      return;
    }

    this.approvalsApi.pending().subscribe({
      next: items => this.pendingApprovals.set(items.length),
      error: () => this.pendingApprovals.set(0)
    });
  }
}

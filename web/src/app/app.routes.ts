import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/auth/http-auth';

export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in',
    loadComponent: () => import('./features/auth/login').then(m => m.LoginPage)
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then(m => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard',
        loadComponent: () => import('./features/dashboard/dashboard').then(m => m.DashboardPage)
      },
      {
        path: 'products',
        title: 'Products',
        loadComponent: () => import('./features/products/product-list').then(m => m.ProductListPage)
      },
      {
        path: 'products/:id',
        title: 'Product',
        loadComponent: () => import('./features/products/product-detail').then(m => m.ProductDetailPage)
      },
      {
        path: 'components',
        title: 'Components',
        loadComponent: () => import('./features/components/component-list').then(m => m.ComponentListPage)
      },
      {
        path: 'components/:id',
        title: 'Component',
        loadComponent: () => import('./features/components/component-detail').then(m => m.ComponentDetailPage)
      },
      {
        path: 'changes',
        title: 'Engineering changes',
        loadComponent: () => import('./features/changes/change-list').then(m => m.ChangeListPage)
      },
      {
        path: 'changes/new',
        title: 'New engineering change',
        canActivate: [roleGuard('Engineer', 'Admin')],
        loadComponent: () => import('./features/changes/change-create').then(m => m.ChangeCreatePage)
      },
      {
        path: 'changes/:id',
        title: 'Engineering change',
        loadComponent: () => import('./features/changes/change-detail').then(m => m.ChangeDetailPage)
      },
      {
        path: 'approvals',
        title: 'My approvals',
        canActivate: [roleGuard('Engineer', 'Approver', 'Admin')],
        loadComponent: () => import('./features/approvals/approval-inbox').then(m => m.ApprovalInboxPage)
      },
      {
        path: 'workflows',
        title: 'Approval workflows',
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/workflows/workflow-list').then(m => m.WorkflowListPage)
      },
      {
        path: 'audit',
        title: 'Audit log',
        canActivate: [roleGuard('Approver', 'Admin')],
        loadComponent: () => import('./features/audit/audit-log').then(m => m.AuditLogPage)
      },
      {
        path: 'users',
        title: 'Users',
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/users/user-list').then(m => m.UserListPage)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];

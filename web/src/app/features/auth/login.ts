import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { describeHttpError } from '../../core/auth/http-auth';
import { UserRole } from '../../core/models';

interface DemoAccount {
  email: string;
  name: string;
  role: UserRole;
}

const DEMO_PASSWORD = 'ForgeFlow!2026';

const DEMO_ACCOUNTS: DemoAccount[] = [
  { email: 'admin@forgeflow.local', name: 'Claire Dubois', role: 'Admin' },
  { email: 'engineer@forgeflow.local', name: 'Elena Petrova', role: 'Engineer' },
  { email: 'engineer2@forgeflow.local', name: 'Marco Rossi', role: 'Engineer' },
  { email: 'approver@forgeflow.local', name: 'Quentin Martin', role: 'Approver' },
  { email: 'approver2@forgeflow.local', name: 'Hannah Schmidt', role: 'Approver' },
  { email: 'viewer@forgeflow.local', name: 'Victor Lee', role: 'Viewer' }
];

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly demoAccounts = DEMO_ACCOUNTS;
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly hidePassword = signal(true);
  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  constructor() {
    if (this.auth.token()) {
      void this.router.navigateByUrl('/dashboard');
    }
  }

  protected useDemoAccount(account: DemoAccount): void {
    this.form.setValue({ email: account.email, password: DEMO_PASSWORD });
    this.error.set(null);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password } = this.form.getRawValue();
    this.loading.set(true);
    this.error.set(null);
    this.auth.login(email, password).subscribe({
      next: () => void this.router.navigateByUrl(this.safeReturnUrl()),
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(
          error.status === 401
            ? 'Invalid email or password.'
            : error.status === 429
              ? 'Too many sign-in attempts. Wait a minute and try again.'
              : describeHttpError(error)
        );
      }
    });
  }

  /** Only follow in-app paths, never an absolute or protocol-relative URL. */
  private safeReturnUrl(): string {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    return returnUrl?.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/dashboard';
  }
}

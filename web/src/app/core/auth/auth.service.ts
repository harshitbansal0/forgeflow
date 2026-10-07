import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, map } from 'rxjs';
import { API_BASE } from '../api/http';
import { LoginResponse, UserInfo, UserRole } from '../models';

interface Session {
  token: string;
  expiresAtUtc: string;
  user: UserInfo;
}

const STORAGE_KEY = 'forgeflow.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly session = signal<Session | null>(restoreSession());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly canEdit = computed(() => this.hasRole('Engineer', 'Admin'));
  readonly canApprove = computed(() => this.hasRole('Engineer', 'Approver', 'Admin'));
  readonly canViewAudit = computed(() => this.hasRole('Approver', 'Admin'));
  readonly isAdmin = computed(() => this.hasRole('Admin'));

  login(email: string, password: string): Observable<UserInfo> {
    return this.http.post<LoginResponse>(`${API_BASE}/auth/login`, { email, password }).pipe(
      map(response => {
        const session: Session = { token: response.accessToken, expiresAtUtc: response.expiresAtUtc, user: response.user };
        localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
        this.session.set(session);
        return response.user;
      })
    );
  }

  /** Returns the bearer token, or null once it has expired. */
  token(): string | null {
    const session = this.session();
    if (!session) {
      return null;
    }

    if (isExpired(session)) {
      this.clear();
      return null;
    }

    return session.token;
  }

  hasRole(...roles: UserRole[]): boolean {
    const user = this.user();
    return !!user && roles.includes(user.role);
  }

  logout(returnUrl?: string): void {
    this.clear();
    void this.router.navigate(['/login'], returnUrl ? { queryParams: { returnUrl } } : undefined);
  }

  private clear(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
  }
}

function isExpired(session: Session): boolean {
  return new Date(session.expiresAtUtc).getTime() <= Date.now();
}

function restoreSession(): Session | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    const session = raw ? (JSON.parse(raw) as Session) : null;
    return session && !isExpired(session) ? session : null;
  } catch {
    return null;
  }
}

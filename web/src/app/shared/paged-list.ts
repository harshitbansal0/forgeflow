import { DestroyRef, Signal, WritableSignal, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormControl } from '@angular/forms';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { Observable, catchError, debounceTime, distinctUntilChanged, map, of, switchMap, tap } from 'rxjs';
import { QueryParams, QueryValue } from '../core/api/http';
import { PagedResult, emptyPage } from '../core/models';

/**
 * Server-side paging, sorting and filtering for a MatTable.
 * Create it in a field initializer so it runs in the component's injection context.
 */
export class PagedList<T> {
  readonly pageIndex = signal(0);
  readonly pageSize = signal(20);
  readonly sort: WritableSignal<Sort>;
  readonly filters: WritableSignal<QueryParams>;
  readonly loading = signal(true);
  readonly result = signal<PagedResult<T>>(emptyPage<T>());
  readonly items: Signal<T[]> = computed(() => this.result().items);

  private readonly version = signal(0);
  private readonly destroyRef = inject(DestroyRef);

  constructor(fetch: (query: QueryParams) => Observable<PagedResult<T>>, defaultSort: Sort, initialFilters: QueryParams = {}) {
    this.sort = signal(defaultSort);
    this.filters = signal(initialFilters);

    const query = computed<QueryParams>(() => {
      this.version();
      const sort = this.sort();
      return {
        ...this.filters(),
        page: this.pageIndex() + 1,
        pageSize: this.pageSize(),
        sortBy: sort.direction ? sort.active : undefined,
        sortDirection: sort.direction || undefined
      };
    });

    toObservable(query)
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap(q => fetch(q).pipe(catchError(() => of(emptyPage<T>())))),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(result => {
        this.result.set(result);
        this.loading.set(false);
      });
  }

  setFilter(key: string, value: QueryValue): void {
    this.filters.update(filters => ({ ...filters, [key]: value }));
    this.pageIndex.set(0);
  }

  connectSearch(control: FormControl<string>): void {
    control.valueChanges
      .pipe(
        debounceTime(300),
        map(value => value.trim()),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(value => this.setFilter('search', value));
  }

  onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  onSort(sort: Sort): void {
    this.sort.set(sort);
    this.pageIndex.set(0);
  }

  refresh(): void {
    this.version.update(v => v + 1);
  }
}

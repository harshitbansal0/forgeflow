import { HttpParams } from '@angular/common/http';

export const API_BASE = '/api';

export type QueryValue = string | number | boolean | null | undefined;
export type QueryParams = Record<string, QueryValue>;

/** Drops empty values so the API only sees filters the user actually set. */
export function toHttpParams(query: QueryParams): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== null && value !== undefined && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}

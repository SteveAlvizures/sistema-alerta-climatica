import { DOCUMENT } from '@angular/common';
import { inject, Injectable, signal } from '@angular/core';

export type AppTheme = 'light' | 'dark';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly storageKey = 'vigia-rural-theme';
  readonly theme = signal<AppTheme>(this.resolveInitialTheme());

  constructor() {
    this.applyTheme(this.theme());
  }

  toggle(): void {
    const nextTheme: AppTheme = this.theme() === 'light' ? 'dark' : 'light';
    this.theme.set(nextTheme);
    this.applyTheme(nextTheme);
    localStorage.setItem(this.storageKey, nextTheme);
  }

  private resolveInitialTheme(): AppTheme {
    const storedTheme = localStorage.getItem(this.storageKey);
    if (storedTheme === 'light' || storedTheme === 'dark') {
      return storedTheme;
    }

    return matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }

  private applyTheme(theme: AppTheme): void {
    this.document.documentElement.dataset['theme'] = theme;
    this.document.documentElement.style.colorScheme = theme;
  }
}

import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { ThemeService } from '../../../../core/services/theme.service';

@Component({ selector: 'app-login-page', imports: [FormsModule, RouterLink], templateUrl: './login-page.html', styleUrl: './login-page.scss' })
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly theme = inject(ThemeService);
  protected readonly submitting = signal(false);
  protected readonly error = signal('');
  protected username = '';
  protected password = '';

  protected submit(): void {
    if (this.submitting()) return;
    this.error.set(''); this.submitting.set(true);
    this.auth.login({ username: this.username, password: this.password }).pipe(
      finalize(() => this.submitting.set(false)),
    ).subscribe({
      next: () => void this.router.navigateByUrl('/'),
      error: (response: HttpErrorResponse) => this.error.set(response.status === 401
        ? 'El usuario o la contraseña no son correctos.'
        : 'No fue posible iniciar sesión. Comprueba la conexión e inténtalo de nuevo.'),
    });
  }
}

import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService, UserRole } from '../../core/auth/auth.service';
import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

function passwordMatchValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    if (!control.get('password') || !control.get('passwordConfirm')) {
      return null;
    }

    const password = control.get('password')?.value;
    const passwordConfirm = control.get('passwordConfirm')?.value;

    if (!password || !passwordConfirm) {
      return null;
    }

    return password === passwordConfirm ? null : { passwordMismatch: true };
  };
}

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RegisterComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly isSubmitting = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly form = this.formBuilder.nonNullable.group(
    {
      username: ['', [Validators.required, Validators.minLength(2)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(4)]],
      passwordConfirm: ['', [Validators.required, Validators.minLength(4)]],
      role: this.formBuilder.nonNullable.control<UserRole>('user', [Validators.required])
    },
    { validators: passwordMatchValidator }
  );

  protected submit(): void {
    if (this.form.invalid || this.isSubmitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set('');
    this.isSubmitting.set(true);

    const { username, password, role } = this.form.getRawValue();

    this.authService
      .login({ username, password, role })
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (session) => {
          const destination = session.role === 'admin' ? ['/admin/games'] : ['/games'];
          void this.router.navigate(destination);
        },
        error: () => {
          this.errorMessage.set('Nao foi possivel registar. Tente novamente.');
        }
      });
  }
}


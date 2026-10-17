import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';

import { AuthService } from '../auth.service';
import { Card } from '../../shared/components/card/card';

const passwordMatchValidator: ValidatorFn = (group: AbstractControl) => {
  const password = group.get('password')?.value as string;
  const confirm = group.get('confirmPassword')?.value as string;
  return password === confirm ? null : { passwordMismatch: true };
};

@Component({
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    MessageModule,
    PasswordModule,
    Card,
  ],
  templateUrl: './register.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Register {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);

  form = this.fb.group({
    userName: ['', [Validators.required, Validators.minLength(3)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required],
  }, { validators: [passwordMatchValidator] });

  loading = signal(false);
  errorMessage = signal<string | null>(null);

  onSubmit() {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.loading.set(true);
    this.errorMessage.set(null);

    const { userName, email, password } = this.form.getRawValue();
    this.authService.register({ userName: userName!, email: email!, password: password! }).subscribe({
      next: () => {
        // Backend returns a token on register — user is already logged in.
        this.router.navigate(['/dashboard']);
      },
      error: (e: HttpErrorResponse) => {
        const errors = e.error?.errors as string[] | undefined;
        this.errorMessage.set(errors?.join(' ') ?? 'Registration failed. Please try again.');
        this.loading.set(false);
      },
    });
  }

  get passwordMismatch(): boolean {
    return this.form.hasError('passwordMismatch')
      && (this.form.controls.confirmPassword.touched ?? false);
  }
}

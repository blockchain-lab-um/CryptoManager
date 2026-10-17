import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';

import { AuthService } from '../auth.service';
import { WebAuthnService } from '../webauthn/webauthn.service';
import { Card } from '../../shared/components/card/card';

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
  templateUrl: './login.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Login {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  form = this.fb.group({
    userName: ['', Validators.required],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  loading = signal(false);
  errorMessage = signal<string | null>(null);

  readonly passkeySupported = WebAuthnService.isSupported();

  async signInWithPasskey() {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      // If the username field is blank/whitespace, omit it so the backend issues a discoverable-credentials
      // assertion (empty allowList → usernameless login). Treating "" as a username would force allow-list mode
      // and fail for users who only know their passkey, not their account name.
      const raw = this.form.value.userName;
      const userName = typeof raw === 'string' && raw.trim().length > 0 ? raw.trim() : undefined;
      await this.authService.loginWithPasskey(userName);
      // Match the existing password-login redirect: read returnUrl from the activated route snapshot.
      // The Login component has no `returnUrl()` signal — do NOT introduce one for this feature.
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
      this.router.navigateByUrl(returnUrl);
    } catch (e: unknown) {
      const message = e instanceof Error ? e.message : 'Passkey sign-in failed.';
      this.errorMessage.set(message);
    } finally { this.loading.set(false); }
  }

  onSubmit() {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.loading.set(true);
    this.errorMessage.set(null);

    const { userName, password } = this.form.getRawValue();
    this.authService.login({ userName: userName!, password: password! }).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/dashboard';
        this.router.navigateByUrl(returnUrl);
      },
      error: (e: HttpErrorResponse) => {
        this.errorMessage.set(e.error?.error ?? 'Login failed. Please check your credentials.');
        this.loading.set(false);
      },
    });
  }
}

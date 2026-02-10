import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';

export interface Saml2Settings {
  tenantId: string;
  isEnabled: boolean;
  idpEntityId?: string;
  idpSsoServiceUrl?: string;
  spEntityId?: string;
  spAcsUrl?: string;
  nameIdFormat?: string;
  attributeMapping?: string;
  signAuthnRequest: boolean;
  requireSignedResponse: boolean;
  requireEncryptedAssertion: boolean;
  clockSkewMinutes: number;
}

export interface CreateOrUpdateSaml2SettingsRequest {
  isEnabled: boolean;
  idpEntityId?: string;
  idpSsoServiceUrl?: string;
  idpCertificate?: string;
  spEntityId?: string;
  spCertificate?: string;
  spCertificatePrivateKey?: string;
  nameIdFormat?: string;
  attributeMapping?: string;
  signAuthnRequest?: boolean;
  requireSignedResponse?: boolean;
  requireEncryptedAssertion?: boolean;
  clockSkewMinutes?: number;
}

@Injectable({
  providedIn: 'root'
})
export class Saml2Service {
  private readonly API_URL = '/saml2';
  private http = inject(HttpClient);

  getSettings(tenantId: string): Observable<Saml2Settings | null> {
    return this.http.get<Saml2Settings>(`${this.API_URL}/settings/${tenantId}`).pipe(
      catchError((err) => (err.status === 404 ? of(null) : throwError(() => err)))
    );
  }

  createOrUpdateSettings(tenantId: string, request: CreateOrUpdateSaml2SettingsRequest): Observable<Saml2Settings> {
    return this.http.post<Saml2Settings>(`${this.API_URL}/settings/${tenantId}`, request);
  }

  deleteSettings(tenantId: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.API_URL}/settings/${tenantId}`);
  }
}

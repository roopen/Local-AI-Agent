/* generated using openapi-typescript-codegen -- do not edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { AttestationConveyancePreference } from './AttestationConveyancePreference';
import type { AttestationStatementFormatIdentifier } from './AttestationStatementFormatIdentifier';
import type { AuthenticationExtensionsClientInputs } from './AuthenticationExtensionsClientInputs';
import type { AuthenticatorSelection } from './AuthenticatorSelection';
import type { Fido2User } from './Fido2User';
import type { PubKeyCredParam } from './PubKeyCredParam';
import type { PublicKeyCredentialDescriptor } from './PublicKeyCredentialDescriptor';
import type { PublicKeyCredentialHint } from './PublicKeyCredentialHint';
import type { PublicKeyCredentialRpEntity } from './PublicKeyCredentialRpEntity';
export type CredentialCreateOptions = {
    rp: PublicKeyCredentialRpEntity;
    user: Fido2User;
    challenge: string | null;
    pubKeyCredParams: Array<PubKeyCredParam> | null;
    timeout?: number;
    attestation?: AttestationConveyancePreference;
    attestationFormats?: Array<AttestationStatementFormatIdentifier> | null;
    authenticatorSelection?: AuthenticatorSelection;
    hints?: Array<PublicKeyCredentialHint> | null;
    excludeCredentials?: Array<PublicKeyCredentialDescriptor> | null;
    extensions?: AuthenticationExtensionsClientInputs;
};


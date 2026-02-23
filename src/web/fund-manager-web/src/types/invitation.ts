export interface Invitation {
  id: string;
  fundId: string;
  targetContact: string;
  invitedBy: string;
  status: InvitationStatus;
  expiresAt: string;
  createdAt: string;
  respondedAt?: string;
}

export type InvitationStatus = 'Pending' | 'Accepted' | 'Declined' | 'Expired';

export interface InviteMemberRequest {
  targetContact: string;
}

export interface AcceptInvitationRequest {
  monthlyContributionAmount: number;
}

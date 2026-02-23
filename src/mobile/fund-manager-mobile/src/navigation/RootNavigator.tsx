import React from 'react';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import HomeScreen from '../screens/HomeScreen';
import FundListScreen from '../screens/funds/FundListScreen';
import FundDetailScreen from '../screens/funds/FundDetailScreen';
import MemberListScreen from '../screens/members/MemberListScreen';
import AcceptInvitationScreen from '../screens/invitations/AcceptInvitationScreen';
import MyLoansScreen from '../screens/loans/MyLoansScreen';
import RequestLoanScreen from '../screens/loans/RequestLoanScreen';
import RepaymentScreen from '../screens/loans/RepaymentScreen';
import CastVoteScreen from '../screens/loans/CastVoteScreen';
import DissolutionScreen from '../screens/dissolution/DissolutionScreen';
import ReportsScreen from '../screens/reports/ReportsScreen';
import NotificationFeedScreen from '../screens/notifications/NotificationFeedScreen';
import NotificationPreferencesScreen from '../screens/notifications/NotificationPreferencesScreen';

export type RootStackParamList = {
  Main: undefined;
  FundDetail: { fundId: string };
  MemberList: { fundId: string; fundName?: string };
  AcceptInvitation: { invitationId: string; fundName?: string; minimumContribution?: number };
  MyLoans: { fundId: string };
  RequestLoan: { fundId: string };
  LoanDetail: { fundId: string; loanId: string };
  Repayments: { fundId: string; loanId: string };
  Voting: { fundId: string; loanId: string };
  Dissolution: { fundId: string };
  Reports: { fundId: string };
  Notifications: undefined;
  NotificationPreferences: undefined;
};

export type MainTabParamList = {
  Home: undefined;
  Funds: undefined;
};

const Stack = createNativeStackNavigator<RootStackParamList>();
const Tab = createBottomTabNavigator<MainTabParamList>();

function MainTabs() {
  return (
    <Tab.Navigator
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: '#1E40AF',
      }}
    >
      <Tab.Screen name="Home" component={HomeScreen} />
      <Tab.Screen name="Funds" component={FundListScreen} />
    </Tab.Navigator>
  );
}

export default function RootNavigator() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false }}>
      <Stack.Screen name="Main" component={MainTabs} />
      <Stack.Screen
        name="FundDetail"
        component={FundDetailScreen as any}
        options={{ headerShown: true, title: 'Fund Details' }}
      />
      <Stack.Screen
        name="MemberList"
        component={MemberListScreen as any}
        options={{ headerShown: true, title: 'Members' }}
      />
      <Stack.Screen
        name="AcceptInvitation"
        component={AcceptInvitationScreen as any}
        options={{ headerShown: true, title: 'Accept Invitation' }}
      />
      <Stack.Screen
        name="MyLoans"
        component={MyLoansScreen as any}
        options={{ headerShown: true, title: 'My Loans' }}
      />
      <Stack.Screen
        name="RequestLoan"
        component={RequestLoanScreen as any}
        options={{ headerShown: true, title: 'Request Loan' }}
      />
      <Stack.Screen
        name="Repayments"
        component={RepaymentScreen as any}
        options={{ headerShown: true, title: 'Repayments' }}
      />
      <Stack.Screen
        name="Voting"
        component={CastVoteScreen as any}
        options={{ headerShown: true, title: 'Loan Voting' }}
      />
      <Stack.Screen
        name="Dissolution"
        component={DissolutionScreen as any}
        options={{ headerShown: true, title: 'Fund Dissolution' }}
      />
      <Stack.Screen
        name="Reports"
        component={ReportsScreen as any}
        options={{ headerShown: true, title: 'Reports' }}
      />
      <Stack.Screen
        name="Notifications"
        component={NotificationFeedScreen}
        options={{ headerShown: true, title: 'Notifications' }}
      />
      <Stack.Screen
        name="NotificationPreferences"
        component={NotificationPreferencesScreen}
        options={{ headerShown: true, title: 'Notification Settings' }}
      />
    </Stack.Navigator>
  );
}

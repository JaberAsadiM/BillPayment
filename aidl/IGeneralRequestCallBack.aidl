// IGeneralRequestCallBack.aidl
package com.persianswitch.smartpos.aidl;

// Declare any non-default types here with import statements

interface IGeneralRequestCallBack {

void onPreLaunch(String hostRequest);

void coreServiceIsNotReady();

void onResponseReceived(String hostResponse,String hostResponseSign);

void onError(in int errorCode, String message);

}

// IPOSMerchantInfoCallBack.aidl
package com.persianswitch.smartpos.aidl;


import com.persianswitch.smartpos.aidl.POSMerchantInfo;


interface IPOSMerchantInfoCallBack {


       void dataUpdate(in POSMerchantInfo data);


       void dataNeedUpdate();


       void dateUpdateFailed(String errorMessage,int errorCode);

}

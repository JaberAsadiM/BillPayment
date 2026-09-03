package com.persianswitch.smartpos.aidl;

import com.persianswitch.smartpos.aidl.POSMerchantInfo;
import com.persianswitch.smartpos.aidl.POSTransactionInquiryResult;
import com.persianswitch.smartpos.aidl.IGeneralRequestCallBack;
import com.persianswitch.smartpos.aidl.IPOSMerchantInfoCallBack;
import com.persianswitch.smartpos.aidl.IPOSPrintStatusCallBack;
import android.graphics.Bitmap;
import java.util.List;

interface IPosService {

 //---------------  indirect operations ------------------
 //-------------------------------------------------------

  /**
   * Starts of the payment solutions(Purchase,Bill,Sim Charge(Voucher),...) provided by the POS
   * Service.
   * Accepted values and formats for the parameters has been described in AsanPardakht POS Service
   * Communication Document.
   * @param transactionCode determines witch payment solution the caller wants to run
   * @param hostRequest includes json string the host request
   * @param hostSign includes the sign of the request
   * @param hostId includes host id of the caller
   * @param lang sdk current language
   * @return a Pending Intent for an activity that presents UI and Logic of the requested payment
   * solution. Caller must show this pending intent to the user with requesting its result(more info
   * in AsanPardakht POS Service Communication Document)
   * @throws RemoteException
   */
 PendingIntent startTransaction(
               int transactionCode,
               String hostRequest,
               String hostSign,
               int hostId,
               String lang
   );

  /**
   * Starts of the payment solutions(Purchase,Bill,Sim Charge(Voucher),...) provided by the POS
   * Service with card no filtere.
   * Accepted values and formats for the parameters has been described in AsanPardakht POS Service
   * Communication Document.
   * @param transactionCode determines witch payment solution the caller wants to run
   * @param hostRequest includes json string the host request
   * @param hostSign includes the sign of the request
   * @param hostId includes host id of the caller
   * @param lang sdk current language
   * @param filterList list of card to block or accept
   * @param isBlacklist if true means card list is black list for current transaction, else otherwise
   * @return a Pending Intent for an activity that presents UI and Logic of the requested payment
   * solution. Caller must show this pending intent to the user with requesting its result(more info
   * in AsanPardakht POS Service Communication Document)
   * @throws RemoteException
   */
 PendingIntent startFilteredTransaction(
               int transactionCode,
               String hostRequest,
               String hostSign,
               int hostId,
               String lang,
               in List<String> filterList,
               boolean isBlacklist
   );

   /**
    * Sends a request from Host App to the AsanPardakht POS Service without doing any logical process
    * in the while.
    * For more information about why this request may be necessary and also accepted values and
    * formats for the parameters please read AsanPardakht POS Service Communication Document.
    * @param hostRequest includes json string of the request for sending to the Asan's Servers
    * @param hostSign sign of the request
    * @param hostId includes host id of the caller
    * @param callBack . The callback that will be call after receiving result.
    */
 void sendGeneralService(
      String hostRequest,
      String hostSign,
      int hostId,
      IGeneralRequestCallBack callBack
 );

  /**
   * Host app use this method for inquiring necessary data before requesting POS Service for
   * starting a payment solution.
   * More information about in witch cases this method should be called before startTransaction and
   * also accepted values and formats for the parameters read AsanPardakht POS Service Communication
   * Document.
   * @param inquiryCode include code of the requested data
   * @param hostRequest includes json request string of hostRequest
   * @param hostSign sign of the request
   * @param hostId includes host id of the caller
   * @param callBack  The callback that will be call after receiving result.
   */
 void sendPOSInquiryService(
      int inquiryCode,
      String hostRequest,
      String hostSign,
      int hostId,
      IGeneralRequestCallBack callBack
  );


 //---------------  direct operations ------------------
 //-------------------------------------------------------

  /**
   *
   * @param hostId the host id of the caller app
   * @param callback returns the updates for return value will send to this callback(more info at document)
   * @return the latest info will be given as return type
   * @throws RemoteException
   */
 POSMerchantInfo getMerchantInfo(
      int hostId,
      IPOSMerchantInfoCallBack callback
  );

  /**
   * In order to inquiry the result of a transaction that its status is not clear for th host use
   * this method.(For more info about the method and its value read AsanPardakht POS Service
   * Communication Document)
   * @param hostTranId transaction id of the transaction
   * @return
   */
 POSTransactionInquiryResult getTransactionStatus(long hostTranId);

  /**
   * @return serial number of the device as a string
   * @throws RemoteException
   */
 String getDeviceSerialNumber();

  /**
   * In order to show POS menu of the device use this method
   * @return returns a pending intent for the activity of the menu
   * @throws RemoteException
   */
 PendingIntent openPosMenu(String lang);


  /**
   * In order to show POS Supervisor menu of the device use this method
   * @return returns a pending intent for the activity of the menu
   * @throws RemoteException
   */
 PendingIntent openSupervisorMenu(String lang);

 PendingIntent openSupervisorSetting(String lang, int supervisorType, String appVersion);


  /**
   * In order to show Merchant wallet use this method
   * @return returns a pending intent for the activity of the menu
   * @throws RemoteException
   */
 PendingIntent startWalletActivity(String lang);

 /**
  * In order to show POS store menu use this method
  * @param lang current language
  * @return returns a pending intent for the activity of the menu
  */
 PendingIntent openStoreMenu(String lang);

  /**
   * change sdk language
   * @param lang sdk current language
   */
 void setLanguage(String lang);

  /**
   * In order to print bitmap sent by merchent
   * @bitmapBytes the printing image bytes
   * @callBack print status callback
   * @return void
   * @throws RemoteException
   */
 void bitmapPrint(String lang,in Bitmap bitmap,int printId, IPOSPrintStatusCallBack callBack);


}